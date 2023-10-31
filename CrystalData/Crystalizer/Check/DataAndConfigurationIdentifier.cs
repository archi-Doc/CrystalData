// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Check;

[TinyhandObject]
public readonly partial struct DataAndConfigurationIdentifier : IEquatable<DataAndConfigurationIdentifier>
{
    public DataAndConfigurationIdentifier()
    {
        this.DataTypeName = string.Empty;
    }

    public DataAndConfigurationIdentifier(string dataTypeName, PathConfiguration configuration)
    {
        this.DataTypeName = dataTypeName;
        this.PathConfiguration = configuration;
    }

    [Key(0)]
    public readonly string DataTypeName;

    [Key(1)]
    public readonly PathConfiguration? PathConfiguration;

    public override int GetHashCode()
        => HashCode.Combine(this.DataTypeName, this.PathConfiguration);

    public bool Equals(DataAndConfigurationIdentifier other)
    {
        if (!this.DataTypeName.Equals(other.DataTypeName))
        {
            return false;
        }

        if (this.PathConfiguration is null)
        {
            if (other.PathConfiguration is null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            if (other.PathConfiguration is not null)
            {
                return this.PathConfiguration.Equals(other.PathConfiguration);
            }
            else
            {
                return false;
            }
        }
    }
}
