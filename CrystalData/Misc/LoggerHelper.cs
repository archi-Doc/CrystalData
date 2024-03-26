// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

internal static class LoggerHelper
{
    public static void Log(this ILogWriter logger, ulong hash)
        => logger.Log(HashedString.Get(hash));

    public static void Log(this ILogWriter logger, ulong hash, object obj1)
        => logger.Log(HashedString.Get(hash, obj1));

    public static void Log(this ILogWriter logger, ulong hash, object obj1, object obj2)
        => logger.Log(HashedString.Get(hash, obj1, obj2));
}
