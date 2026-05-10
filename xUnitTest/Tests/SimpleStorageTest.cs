// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Arc.Crypto;
using CrystalData;
using CrystalData.Storage;
using Tinyhand;
using Xunit;

namespace xUnitTest.CrystalDataTest;

public class SimpleStorageTest
{
    [Fact]
    public void Test1()
    {
        /*var assembly = typeof(CrystalControl).Assembly;
        var type = assembly.GetType("CrystalData.Storage.SimpleStorageData", throwOnError: true)!;
        var simpleStorage = Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: null,
            culture: null)!;*/

        var data = new SimpleStorageData();
        var st = TinyhandSerializer.SerializeToString(data);

        var data2 = TinyhandSerializer.DeserializeFromString<SimpleStorageData>(st);
        data.EqualsForTest(data2).IsTrue();

        uint file = 0;
        data.Put(ref file, 1);
        file = 0;
        data.Put(ref file, 333);
        file = 0;
        data.Put(ref file, 4444);
        data.Remove(file);

        st = TinyhandSerializer.SerializeToString(data);
        data2 = TinyhandSerializer.DeserializeFromString<SimpleStorageData>(st);
        data.EqualsForTest(data2).IsTrue();
    }
}
