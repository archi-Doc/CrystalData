# CrystalData Rename

This document lists public API names renamed in CrystalData. Use it to update projects that reference CrystalData.

## Compatibility

- Source-breaking renames only. No behavior changes.
- Serialized `CrystalConfiguration` data is still compatible. `IsVolatile` and `NumberOfHistoryFiles` keep their old serialization keys (`Volatile`, `NumberOfFileHistories`).
- Parameter renames only affect callers that use named arguments.
- Implementers of `ICrystalDataQuery`, `IFiler`, `ISingleFiler`, and `IStorage` must update the member names in their implementations.

## Types

| Old | New |
| --- | --- |
| `PathConfiguration.Type` (nested enum) | `PathConfiguration.PathKind` |

## Properties, fields, constants, and enum members

| Declaring type | Old | New |
| --- | --- | --- |
| `CrystalConfiguration` | `Volatile` | `IsVolatile` |
| `CrystalConfiguration` | `NumberOfFileHistories` | `NumberOfHistoryFiles` |
| `CrystalConfiguration` | `HasFileHistories` | `HasHistoryFiles` |
| `CrystalOptions` | `ConcurrentUnload` | `MaxConcurrentUnloads` |
| `CrystalOptions` | `DefaultBackup` | `DefaultBackupDirectory` |
| `PathConfiguration` | `PathType` | `Kind` |
| `IFiler`, `ISingleFiler` | `SupportPartialWrite` | `SupportsPartialWrite` |
| `CrystalFiler` | `IsProtected` | `HasHistoryFiles` |
| `CrystalObjectResult<T>` | `Object` | `Data` |
| `StorageMap` | `Filename` (const) | `FileName` |
| `CrystalSource` | `NoSource` | `None` |
| `CrystalDataHashed.CrystalDataQueryDefault` | `NoCheckFile` (string key) | `NoSupplementFile` |

## Methods

| Declaring type | Old | New |
| --- | --- | --- |
| `ICrystalDataQuery` | `NoCheckFile()` | `NoSupplementFile()` |
| `CrystalControl` | `TestJournalAll()` | `TestAllJournals()` |
| `StorageId` | `TryParse(ReadOnlySpan<byte>, out StorageId)` | `TryRead(ReadOnlySpan<byte>, out StorageId)` |
| `StorageHelper` | `ByteToString(long)` | `FormatByteSize(long)` |
| `StorageHelper` | `EndsWith_SlashInsensitive(string, string)` | `EndsWithSlashInsensitive(string, string)` |
| `StorageHelper` | `GetPathNotRoot(string)` | `GetPathWithoutRoot(string)` |
| `LocalFiler` | `Check(CrystalControl, string)` | `CheckDirectory(CrystalControl, string)` |

`StorageId.TryParse(string, out StorageId)` is unchanged. Byte input now uses `TryRead`, matching `Waypoint.TryRead`.

## Parameters

| Member | Old | New |
| --- | --- | --- |
| `FileConfiguration.AppendPath` and overrides | `file` | `suffix` |
| `IStorage.PutAndForget`, `IStorage.PutAsync` | `memoryToBeShared` | `dataToBeShared` |
| `MonoData<TIdentifier, TDatum>.Set`, `TryGet` | `datum` | `value` |
| `CrystalFiler.Save` | `rentMemory` | `rentedMemory` |
| `SerializeHelper.Serialize` | `rentMemory` | `rentedMemory` |
| `SimpleJournalConfiguration` constructor | `configuration` | `directoryConfiguration` |
| `SimpleStorageConfiguration` constructor | `configuration`, `backupConfiguration` | `directoryConfiguration`, `backupDirectoryConfiguration` |
| `Waypoint`, `StorageId` operators `<`, `>` | `w1`, `w2` | `left`, `right` |

## Migration notes

- These names are distinct enough for whole-word search and replace: `NumberOfFileHistories`, `HasFileHistories`, `SupportPartialWrite`, `TestJournalAll`, `NoCheckFile`, `ConcurrentUnload`, `EndsWith_SlashInsensitive`, `GetPathNotRoot`, `ByteToString`.
- Replace these only in context, because the old names are common words: `Volatile`, `DefaultBackup`, `Object`, `Type`, `PathType`, `IsProtected`, `Filename`, `NoSource`, `Check`, `TryParse`.
