// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public enum CrystalResult
{
    Success,
    NotStarted,
    Started,
    NotPrepared,
    Aborted,

    Deleted, // Checked
    NotFound, // Checked
    CorruptedData, // Checked
    DeserializationFailed, // Checked
    SerializationFailed, // Checked
    FileOperationError, // Checked
    NoPartialWriteSupport, // Checked
    NoAccess, // Checked
    DataIsLocked,
    DataIsObsolete,
}
