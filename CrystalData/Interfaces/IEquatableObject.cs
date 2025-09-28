// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

/// <summary>
/// An interface for comparing objects.<br/>
/// Normally, objects are serialized with Tinyhand and their byte sequences are compared.<br/>
/// However, that approach cannot be used for order-insensitive collections (e.g., hash-based collections).<br/>
/// Implement this interface for such cases.
/// </summary>
public interface IEquatableObject
{
    bool ObjectEquals(object other);
}
