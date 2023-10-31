// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public enum CrystalResult
{
    Success,
    NotStarted,
    Started,
    NotPrepared,
    Aborted,

    OverSizeLimit, // Checked
    OverNumberLimit, // Checked
    DatumNotRegistered, // Checked
    Deleted, // Checked
    NotFound, // Checked
    CorruptedData, // Checked
    SerializeError, // Checked
    DeserializeError, // Checked
    FileOperationError, // Checked
    NoPartialWriteSupport, // Checked
    NoAccess, // Checked
    DataIsLocked,
    DataIsObsolete,
}
