// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

internal static class LoggerHelper
{
    public static void Write(this LogWriter writer, ulong hash)
        => writer.Write(HashedString.Get(hash));

    public static void Write(this LogWriter writer, ulong hash, object obj1)
        => writer.Write(HashedString.Get(hash, obj1));

    public static void Write(this LogWriter writer, ulong hash, object obj1, object obj2)
        => writer.Write(HashedString.Get(hash, obj1, obj2));
}
