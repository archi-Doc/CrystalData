// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

using System.Collections.Frozen;

namespace CrystalData;

[ValueLinkObject(Isolation = IsolationLevel.Serializable)]
internal abstract partial class CrystalObjectBase
{
    public partial class GoshujinClass
    {
        public ICrystalInternal[] GetCrystals(bool includeUnmanaged)
        {
            using (this.LockObject.EnterScope())
            {
                if (includeUnmanaged)
                {
                    return this.ListChain.Select(x => (ICrystalInternal)x).ToArray();
                }
                else
                {
                    return this.ListChain.Where(x => !x.IsUnmanaged).Select(x => (ICrystalInternal)x).ToArray();
                }
            }
        }

        public FrozenDictionary<uint, ICrystalInternal> GetPlaneDictionary()
        {
            using (this.LockObject.EnterScope())
            {// plane, icrystalinternal
                return this.PlaneChain.ToFrozenDictionary(x => x.Plane, x => (ICrystalInternal)x);
            }
        }

        public KeyValuePair<uint, ICrystalInternal>[] GetPlaneKeyValue()
        {
            using (this.LockObject.EnterScope())
            {
                return this.PlaneChain.Select(x => new KeyValuePair<uint, ICrystalInternal>(x.Plane, (ICrystalInternal)x)).ToArray();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this crystal object is registered in the Unit builder.
    /// </summary>
    public bool IsRegistered { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this crystal object is unmanaged.<br/>
    /// Unmanaged crystals are not subject to Load, Store, or Delete operations by the Crystalizer (except during journal reading).
    /// </summary>
    public bool IsUnmanaged { get; set; }

    [Link(Unique = true, Type = ChainType.Unordered, AutoLink = false)]
    protected uint Plane { get; set; }

    [Link(Type = ChainType.Ordered, AutoLink = false)]
    protected uint TimeForDataSaving { get; set; }

    [Link(Primary = true, Name = "List", Type = ChainType.LinkedList)]
    public CrystalObjectBase()
    {
    }
}
