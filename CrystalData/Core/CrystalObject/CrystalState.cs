// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// Represents the state of a Crystal object.
/// </summary>
public enum CrystalState
{
    /// <summary>
    /// The initial state before preparation.
    /// </summary>
    Initial,

    /// <summary>
    /// The state after the object has been prepared.
    /// </summary>
    Prepared,

    /// <summary>
    /// The state indicating the object has been deleted.
    /// </summary>
    Deleted,
}
