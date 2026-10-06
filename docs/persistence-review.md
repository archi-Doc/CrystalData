# Persistence review

Reviewed on 2026-10-06 with .NET SDK 10.0.401 on Windows x64.

## Scope

The review covered crystal lifecycle and configuration, local/S3 filers, journal recovery, storage points and their metadata, serialization helpers, sample applications, XML documentation, and the README. Existing commented-out code was preserved.

## Corrections

| Area | Corrected behavior |
| --- | --- |
| Crystal lifecycle | Concurrent initial loads share one deserialization. Saves wait for loading; failed lazy loads report an error. Reconfiguring a loaded crystal writes its unchanged data to the new destination. Volatile crystals do not write an initial snapshot. |
| Recovery snapshots | A failed recovery write preserves the original snapshot, and cleanup errors are returned for retry. |
| Shutdown metadata | Startup validates and awaits removal of the clean-shutdown marker and reports metadata preparation failures. |
| Filer queues | Operations on the same canonical local path execute in submission order. Waiting behind another operation counts toward the timeout. Flush includes queued operations. |
| Memory ownership | Completed jobs return to the pool. Timed-out reads and aborted writes release pooled buffers after completion. Cloned single filers retain their timeout. |
| Journals | Reads include buffered records and respect the requested byte range. Recovery rejects truncated records and missing/non-progressing chunks. Interrupted-merge leftovers are reconciled only after checking the replacement's length and hash. |
| Storage points | Read-only acquisition does not create missing data. Deleted points cannot be resurrected through acquisition. Unloaded parents reconnect children before deletion. In-place deserialization discards stale cached point references. Closed generic formatters are registered for structural journal replay without reflection. |
| Storage accounting | Published maps are fully initialized; duplicate maps are not registered twice. Usage is derived from the backing storage. Pin/unpin transitions preserve accounting. Metadata rejects invalid entries and replay updates usage idempotently. |
| Utilities | `MonoData` uses a stable synchronization lock across in-place restoration, validates collection invariants, and avoids allocating entries at zero capacity. Disposed memory streams reject reads. |
| S3 | Reads fill a pooled buffer directly and validate the response length. Missing objects are distinguished from other failures. Bulk deletion checks per-object errors. |
| Samples and docs | Advanced examples check initialization/save results and isolate helper metadata. README and XML comments describe actual lifecycle, ownership, scheduling, and recovery behavior. |

## Allocation reductions

- Reuse completed filer jobs instead of abandoning their pool entries.
- Read S3 responses directly into one rented buffer, removing the `MemoryStream` and `ToArray` copies.
- Parse waypoint, storage ID, and journal book names into stack buffers.
- Avoid temporary task/result arrays for paired storage operations, completed `ValueTask` conversions, journal read lists when data is already resident, and intermediate path substrings.
- Bound save workers to available work and construct crystal snapshots without LINQ iterator/builder allocations.
- Remove unused internal `HashHelper`, `MonoTemplate`, and crystal-plane enumeration code.

These are verified code-path and ownership changes; no end-to-end throughput improvement is claimed without a representative workload benchmark.

## Regression coverage

New tests cover concurrent load/save coordination, destination changes without data changes, volatile initialization, failed lazy loading, metadata retries, marker validation, failed recovery writes, queued timeouts, FIFO flush, path aliases, buffer ownership, pooled-job reuse, exact journal ranges, interrupted merges, storage deletion/replay/accounting, collection restore contention, and malformed serialized metadata.

## Verification results

- Release solution build: **0 warnings, 0 errors**.
- Full test suite: **114 passed, 0 failed, 0 skipped**, including **47 added cases**. The baseline had 67 passing cases.
- Forty lifecycle, queue, storage-integrity, and utility tests passed in three additional runs to check scheduling-sensitive behavior.
- Windows x64 NativeAOT publish succeeded. Running the resulting QuickStart executable twice verified `Id: 0 → 1` on the first run and `Id: 1 → 2` on the second.

Coverage was collected from the Release test executable with `dotnet-coverage`. Package rates include Tinyhand/ValueLink generated code. The hand-written line metric unions hits by source file and line number, excluding `obj` paths so closed generic instantiations are not counted repeatedly.

| Metric | Before | After |
| --- | ---: | ---: |
| CrystalData package line coverage | 58.33% | 60.87% |
| CrystalData package branch coverage | 50.48% | 52.63% |
| Hand-written line coverage | 71.34% (3,445 / 4,829) | 74.16% (3,716 / 5,011) |

Selected hand-written file coverage:

| File | Before | After |
| --- | ---: | ---: |
| `CrystalObject.cs` | 77.07% | 80.37% |
| `FilerBase.cs` | 84.29% | 87.83% |
| `SimpleJournal.cs` | 86.57% | 88.44% |
| `StorageObject.cs` | 85.32% | 85.75% |
| `SimpleStorageData.cs` | 75.51% | 79.17% |
| `MonoData.cs` | 86.46% | 87.62% |
| `S3Filer.cs` | 0.00% | 0.00% |

Coverage gaps remain in live S3 operations, interactive recovery choices, and less common failure paths. These measurements describe exercised code, not proof that all races or corruption scenarios are excluded.

Local verification outputs are under ignored `artifacts/`: `review-build.log`, `review-final-test.log`, `review-baseline.cobertura.xml`, `review-final.cobertura.xml`, `review-contention-*.log`, and `review-nativeaot-*.log`.

## Reproduction

```shell
dotnet build CrystalData.slnx -c Release
dotnet test CrystalData.slnx -c Release --no-build --no-restore
dotnet tool install dotnet-coverage --tool-path artifacts/tools
artifacts/tools/dotnet-coverage collect "dotnet xUnitTest/bin/Release/net10.0/xUnitTest.dll" -f cobertura -o artifacts/review-final.cobertura.xml
```

The Windows sandbox initially denied directory moves in its redirected temporary directory. Pointing `TEMP` and `TMP` at `artifacts/test-temp` allowed the same tests to run without changing their assertions. Building with `-m:1 -p:UseSharedCompilation=false` and `MSBuildEnableWorkloadResolver=false` avoided sandbox-specific MSBuild/workload service issues.

NativeAOT publishing required running MSBuild outside the sandbox because its isolated task host was blocked. It then completed successfully using the cached dependencies.

## Verification limits

- S3 code was compiled against the configured SDK; live bucket/credential/network integration was not exercised.
- Synthetic interrupted-merge and write-failure tests do not simulate every process crash, filesystem failure, or power-loss condition.
- Snapshot writes without histories still overwrite in place. Persistence is not an atomic transaction across crystals, journals, storage files, and backups.
- Journal replay is not transactional; malformed records can be logged after earlier records have changed memory. A successful startup does not certify complete replay.
- Application mutations of root objects, referenced collection values, and singleton observers require application-level synchronization. Cross-process writers and controls sharing writable paths remain unsupported.
