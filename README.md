# CrystalData

[![NuGet](https://img.shields.io/nuget/v/CrystalData)](https://www.nuget.org/packages/CrystalData)
[![Build and Test](https://github.com/archi-Doc/CrystalData/actions/workflows/test.yml/badge.svg)](https://github.com/archi-Doc/CrystalData/actions/workflows/test.yml)

CrystalData is a persistence engine for .NET. It combines snapshot files, optional journals, backups, and independently loaded storage nodes with [Tinyhand](https://github.com/archi-Doc/Tinyhand) serialization and [ValueLink](https://github.com/archi-Doc/ValueLink) collections.

## Contents

- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [NativeAOT](#nativeaot)
- [Configuration](#configuration)
- [Paths and backups](#paths-and-backups)
- [Saving and shutdown](#saving-and-shutdown)
- [Concurrency and durability](#concurrency-and-durability)
- [Journaling](#journaling)
- [Auxiliary storage](#auxiliary-storage)
- [Additional utilities](#additional-utilities)
- [S3 storage](#s3-storage)
- [Recovery](#recovery)
- [Samples](#samples)
- [Testing](#testing)

## Requirements

- .NET 10 SDK or later

## Installation

```shell
dotnet add package CrystalData
```

Package Manager Console:

```powershell
Install-Package CrystalData
```

## Quick start

Define a Tinyhand-serializable data type:

```csharp
using System.ComponentModel;
using Tinyhand;

[TinyhandObject]
public partial class FirstData
{
    [Key(0)]
    public int Id { get; set; }

    [Key(1)]
    [DefaultValue("Hoge")]
    public string Name { get; set; } = "Hoge";
}
```

Register the crystal, prepare the storage services, update the data, and shut down cleanly:

```csharp
using CrystalData;
using Microsoft.Extensions.DependencyInjection;

var product = new CrystalUnit.Builder()
    .ConfigureCrystal(context =>
    {
        context.AddCrystal<FirstData>(
            new CrystalConfiguration(
                new LocalFileConfiguration("Local/FirstData.tinyhand"))
            {
                SaveFormat = SaveFormat.Utf8,
                NumberOfHistoryFiles = 1,
            });
    })
    .Build();

var services = product.Context.ServiceProvider;
var control = services.GetRequiredService<CrystalControl>();
var result = await control.PrepareAndLoad(useQuery: false);
if (result.IsFailure())
{
    throw new InvalidOperationException($"CrystalData initialization failed: {result}");
}

var data = services.GetRequiredData<FirstData>();
data.Id++;
data.Name = "Updated";

await control.StoreAndRip();
```

`StoreAndRip` is terminal: it stops new storage-point lock acquisitions as soon as shutdown begins. Stop application updates before calling it; if it fails, resolve the storage error and retry shutdown.

## NativeAOT

CrystalData supports NativeAOT. Data types must use Tinyhand source generation and be registered through the generic `AddCrystal<TData>`, `CreateCrystal<TData>`, or `GetOrCreateCrystal<TData>` APIs so their closed generic forms are visible at build time.

Publish an application for a specific runtime identifier:

```shell
dotnet publish -c Release -r win-x64 --self-contained -p:PublishAot=true
```

The CrystalData project enables the .NET AOT and trimming compatibility analyzers, and CI publishes and runs QuickStart as a native executable.

## Configuration

Each registered type has a `CrystalConfiguration`.

| Property | Purpose |
| --- | --- |
| `SaveFormat` | Selects binary or UTF-8 Tinyhand output. The default comes from `CrystalOptions.DefaultSaveFormat`. |
| `IsVolatile` | Keeps the crystal in memory without writing snapshot files. |
| `SaveInterval` | Sets the automatic snapshot interval for the crystal. |
| `NumberOfHistoryFiles` | Sets the number of retained snapshot files. Use `0` to disable history files. |
| `FileConfiguration` | Selects the primary snapshot file. |
| `BackupFileConfiguration` | Selects an optional backup snapshot file. |
| `StorageConfiguration` | Configures independently loaded `StoragePoint<T>` data. |
| `RequiredForLoading` | Passes failures for previously stored data to the recovery query. |

Global defaults and limits are configured with `CrystalOptions`:

```csharp
context.SetCrystalOptions(new CrystalOptions
{
    GlobalDirectory = new LocalDirectoryConfiguration("Data"),
    DefaultBackupDirectory = new LocalDirectoryConfiguration("Backup"),
    DefaultSaveFormat = SaveFormat.Binary,
    MemoryUsageLimit = 512L * 1024 * 1024,
});
```

Register more than one crystal when an application has independently persisted data sets:

```csharp
context.AddCrystal<FirstData>(
    new CrystalConfiguration(new GlobalFileConfiguration("First.tinyhand")));
context.AddCrystal<SecondData>(
    new CrystalConfiguration(new GlobalFileConfiguration("Second.tinyhand")));
```

`CreateCrystal<TData>` creates an independent instance on each call. `GetOrCreateCrystal<TData>` returns the registered instance for that type, or atomically creates and registers one. When an instance already exists, the supplied configuration is ignored. Use different paths for independent crystals.

`Configure`, `ConfigureFile`, and `ConfigureStorage` retain cached data. Call the crystal's `PrepareAndLoad` again before saving with the new settings; the next snapshot save writes to the new destination even when the data has not changed. Reconfiguration does not migrate existing auxiliary files. `SaveConfigurations`, `LoadConfigurations`, and `ResetConfigurations` save, apply, and restore the registered persistence settings.

Additional `CrystalOptions` settings:

| Property | Purpose |
| --- | --- |
| `DataDirectory` | Resolves relative local paths, including the default supplement metadata. |
| `GlobalStorage` | Supplies auxiliary storage for crystals using `GlobalStorageConfiguration`. |
| `SaveDelay` | Delays saves requested through the save queue; defaults to one minute. |
| `MemoryUsageLimit` | Triggers eviction of loaded auxiliary data; defaults to 500 MiB. Locked or pinned data can keep usage above the limit. |
| `MaxConcurrentUnloads` | Limits concurrent bulk save/release workers; defaults to eight. |
| `TimeoutUntilForcedRelease` | Controls when a bulk release switches from retrying to forced release; defaults to ten seconds. |
| `FilerTimeout` | Limits how long callers wait for individual file operations; defaults to three seconds. |
| `SupplementFile`, `BackupSupplementFile` | Select primary and backup control metadata files. |

## Paths and backups

- `LocalFileConfiguration` and `LocalDirectoryConfiguration` use absolute paths as-is. Relative paths are resolved against `CrystalOptions.DataDirectory`.
- `GlobalFileConfiguration` and `GlobalDirectoryConfiguration` are resolved relative to `CrystalOptions.GlobalDirectory`.
- `EmptyFileConfiguration` and `EmptyDirectoryConfiguration` disable the corresponding file or directory.
- `S3FileConfiguration` and `S3DirectoryConfiguration` identify objects in an S3 bucket.

Set `BackupFileConfiguration` for one crystal, or set `CrystalOptions.DefaultBackupDirectory` to derive backup locations for crystals, journals, and auxiliary storage that do not define one explicitly.

Snapshot histories provide recovery candidates when the current file is missing or invalid. Journaling requires at least one history file for every journaled crystal.

Controls must not share writable files. Give each control its own `DataDirectory`, or explicitly separate all paths, including `CrystalOptions.SupplementFile` and `BackupSupplementFile`. Changing only `GlobalDirectory` does not relocate the default local supplement file.

## Saving and shutdown

Use the lifecycle method that matches the operation:

| Method | Behavior |
| --- | --- |
| `PrepareAndLoad()` | Prepares persistence services and loads registered crystals. |
| `Store()` | Persists all managed crystals and auxiliary storage. |
| `StoreAndRelease()` | Persists all managed data and attempts to release its resources. |
| `StoreAndRip()` | Persists all managed data, records a clean shutdown, and terminates services. |

For a single crystal, call `ICrystal.StoreData()`. For independently loaded nodes, call `StoragePoint<T>.AddToSaveQueue()` to schedule persistence or `StoragePoint<T>.StoreData()` to request it directly.

Check the `CrystalResult` returned by `PrepareAndLoad` and individual crystal saves. Failed preparation can be retried; `IsPrepared` alone does not confirm that every crystal loaded successfully. `Store`, `StoreAndRelease`, and `StoreAndRip` throw `IOException` for reported persistence failures. Unloaded and deleted crystals are skipped. These operations can also throw cancellation or serialization exceptions.

Accessing `ICrystal<T>.Data` loads it synchronously when necessary and throws `IOException` if preparation or loading fails. Prefer explicit asynchronous preparation during startup. `LoadAllCrystals` loads deferred crystals and throws on a load failure.

Snapshot and auxiliary-data saves await configured backup writes. A failed storage-point write retains its in-memory data and previous storage identifiers for retry. The clean-shutdown marker is written only after the data, journal, and supplement saves succeed.

## Concurrency and durability

Persistence operations are serialized per crystal, and full-control saves are serialized per control. This does not make arbitrary application changes to `ICrystal<T>.Data` thread-safe. Coordinate root-data mutations with saves, or use the appropriate ValueLink isolation and locking model.

Use `StoragePoint<T>.TryLock` for mutations. Dispose its `DataScope<T>` before awaiting a save of that point or any containing crystal. `StoreOnly` and `ForceRelease` wait for the point's lock; `TryRelease` returns `false` if it cannot acquire the lock. Keep a consistent parent-to-child lock order. `TryGet` and `PinData` return references without granting exclusive mutation access; pinning only prevents data eviction.

Saving is not a transaction across crystals, snapshots, journal files, or backups. A failure can leave some writes completed. File-write completion does not guarantee survival of an immediate power loss. With histories disabled, snapshots are overwritten in place. Use histories and separate backups for recoverability, and test your application's recovery policy. Cross-process writers and different controls targeting the same files are not supported.

Low-level filer operations preserve submission order for the same path. A timeout stops the caller from waiting but can leave the queued operation running. Methods ending in `AndForget` report submission, so use their awaited counterparts when completion or failure must be observed. Return pooled read-result memory after use, and do not mutate buffers while shared writes are pending.

## Journaling

Journaling records changes to Tinyhand structural objects between snapshots. Configure a journal and use `[TinyhandObject(Structural = true)]` on journaled data:

```csharp
[TinyhandObject(Structural = true)]
public partial class JournalData
{
    [Key(0)]
    public partial int Count { get; set; }
}

var product = new CrystalUnit.Builder()
    .ConfigureCrystal(context =>
    {
        context.SetJournal(
            new SimpleJournalConfiguration(
                new LocalDirectoryConfiguration("Data/Journal")));

        context.AddCrystal<JournalData>(
            new CrystalConfiguration(
                new LocalFileConfiguration("Data/JournalData.tinyhand"))
            {
                NumberOfHistoryFiles = 3,
            });
    })
    .Build();
```

Structural members must be compatible with Tinyhand's structural serialization rules. ValueLink collections can also be used as journaled roots.

## Auxiliary storage

`StoragePoint<T>` keeps a child object in an independently loaded file. Configure auxiliary storage on the owning crystal:

```csharp
context.AddCrystal<RootData>(
    new CrystalConfiguration(new LocalFileConfiguration("Data/Root.tinyhand"))
    {
        StorageConfiguration = new SimpleStorageConfiguration(
            new LocalDirectoryConfiguration("Data/Storage"))
        {
            NumberOfHistoryFiles = 3,
        },
    });
```

The type containing a storage point must be a Tinyhand structural object. `StoragePoint<T>` reserves Tinyhand key `0`; derived storage-point types must start their own keys at `1`.

Read with `TryGet`. Use `TryLock` whenever data will be changed, and dispose the returned `DataScope<T>` to release the lock:

```csharp
using (var scope = await root.Child.TryLock(AcquisitionMode.GetOrCreate))
{
    if (scope.IsValid)
    {
        scope.Data.Count++;
    }
}

// The mutation lock has been released; saving can now acquire it.
await root.Child.StoreData(StoreMode.StoreOnly);
```

Avoid replacing a storage-point instance with `Set` unless instance replacement is specifically required.

`DeleteData` waits for active data scopes and permanently invalidates the point; later acquisition fails and `PinData` throws. Dispose scopes before deleting. Storage-point queue timing uses `CrystalOptions.SaveDelay`; its per-call delay argument currently has no effect. `StorageControl.StorageUsage` includes retained auxiliary-file histories.

Crystals using the same primary auxiliary-storage directory share a storage instance. Use consistent backup and history settings for that directory. `GlobalStorageConfiguration.Default` selects `CrystalOptions.GlobalStorage`.

## Additional utilities

- `MonoData<TKey, TValue>` is a thread-safe, Tinyhand-serializable bounded collection. Writes refresh eviction order; reads do not. Reducing capacity evicts the oldest entries, and a capacity of zero retains nothing. Synchronization protects the collection, not mutations inside referenced values.
- `RsCoder` encodes and recovers Reed-Solomon shards over GF(256). Any `DataShardCount` intact shards recover the original bytes. Keep the original byte length and shard-availability flags for decoding. Use the span overloads with reusable buffers to reduce allocation. The codec does not detect corrupt shards; identify damaged shards before decoding. It is a standalone utility and is not automatically applied to persisted files.

## S3 storage

Supply bucket credentials through `IStorageKey`, then use an S3 file or directory configuration:

```csharp
var storageKey = services.GetRequiredService<IStorageKey>();
storageKey.AddKey(
    "my-bucket",
    new AccessKeyPair("ACCESS_KEY_ID", "SECRET_ACCESS_KEY"));

var configuration = new CrystalConfiguration(
    new S3FileConfiguration("my-bucket", "app/FirstData.tinyhand"));
```

Do not embed production credentials in source code. Provide them through the application's secret-management mechanism.

## Recovery

`PrepareAndLoad(useQuery: true)` consults the registered `ICrystalDataQuery` when recovery requires a decision. Pass `false` for non-interactive startup behavior. The second argument, `loadCrystals`, can defer loading after a clean shutdown; recovery after an unclean shutdown always loads crystals and replays the journal.

The non-interactive policy accepts backup recovery and continuation after load failures, which can reconstruct default data. To reject failed snapshot loads for previously stored data, set `RequiredForLoading = true`, register an `ICrystalDataQuery` that aborts, and prepare with `useQuery: true`.

CrystalData checks the primary snapshot, available histories, and configured backups. For previously stored data with `RequiredForLoading = true`, a load failure is passed to `ICrystalDataQuery`; initialization fails when the query chooses to abort.

History snapshots and auxiliary data are hash-checked before deserialization. A damaged primary snapshot can fall back to a valid backup with the same waypoint. Recovery queries may choose to reconstruct defaults when data cannot be loaded; review that policy before using unattended startup with valuable data.

Journal replay is not transactional. Malformed-record errors may be logged after earlier records have already changed in-memory data. Test your recovery policy with incomplete and damaged journals; successful initialization alone does not prove that every record was replayed.

## Samples

- [QuickStart](QuickStart) contains the smallest complete application.
- [Advanced](Advanced) covers backups, dynamic configuration, journals, paths, dependency injection, save timing, and storage points.

## Testing

```shell
dotnet test CrystalData.slnx -c Release
```

The persistence regression tests cover concurrent registration and saves, journal append/save contention, failed primary and backup writes, retry behavior, corrupt hashes, short reads, storage-point locks, pinned data, and clean-shutdown metadata. Tests use isolated local files; they do not replace S3 integration tests or process-crash and power-loss testing.

To collect coverage with Microsoft's `dotnet-coverage` tool:

```shell
dotnet tool install dotnet-coverage --tool-path artifacts/tools
artifacts/tools/dotnet-coverage collect "dotnet test CrystalData.slnx -c Release --no-build --no-restore" -f cobertura -o artifacts/coverage.cobertura.xml
```

Build the tests before using `--no-build`. Coverage includes source-generated code; inspect the `CrystalData` package and uncovered hand-written source separately.

See the [persistence review](docs/persistence-review.md) for measured coverage, regression coverage, and verification limits.

## License

CrystalData is licensed under the [MIT License](LICENSE).
