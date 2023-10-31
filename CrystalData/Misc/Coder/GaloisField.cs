// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData;

public class GaloisField
{
    public const int Max = 256;
    public const int Mask = Max - 1;
    public const int FieldGenPoly = 301; // 301 > 285 > 435

    public static GaloisField Get(int fieldGenPoly)
    {
        GaloisField? field;
        if (!fieldCache.TryGetValue(fieldGenPoly, out field))
        {
            field = new GaloisField(fieldGenPoly);
            fieldCache[fieldGenPoly] = field;
        }

        return field;
    }

    private static Dictionary<int, GaloisField> fieldCache = new();

    private GaloisField(int fieldGenPoly)
    {
        this.GF = new byte[Max];
        this.GFI = new byte[Max];
        this.GF[0] = (byte)Mask;
        this.GFI[Mask] = 0;

        var y = 1;
        unchecked
        {
            for (var x = 0; x < Mask; x++)
            {
                this.GF[y] = (byte)x;
                this.GFI[x] = (byte)y;
                y <<= 1;
                if (y >= Max)
                {
                    y = (y ^ fieldGenPoly) & Mask;
                }
            }
        }

        this.Multi = new byte[Max * Max];
        this.Div = new byte[Max * Max];
        for (var a = 0; a < Max; a++)
        {
            for (var b = 0; b < Max; b++)
            {
                var i = (a * Max) + b;
                this.Multi[i] = this.InternalMulti(a, b);
                this.Div[i] = this.InternalDiv(a, b);
            }
        }
    }

    public byte[] GF { get; }

    public byte[] GFI { get; }

    public byte[] Multi { get; }

    public byte[] Div { get; }

    internal byte InternalMulti(int a, int b)
    {
        if (a == 0 || b == 0)
        {
            return 0;
        }

        var c = this.GF[a] + this.GF[b];
        return this.GFI[c % Mask];

        /*if (c < Mask)
        {
            return this.GFI[c];
        }
        else
        {
            return this.GFI[c - Mask];
        }*/
    }

    internal byte InternalDiv(int a, int b)
    {
        if (a == 0)
        {
            return 0;
        }
        else if (b == 0)
        {
            return 0;
        }

        var c = this.GF[a] - this.GF[b];
        return this.GFI[(c + Mask) % Mask];

        /*var gfa = this.GF[a];
        var gfb = this.GF[b];
        if (gfb <= gfa)
        {
            return this.GFI[gfa - gfb];
        }
        else
        {
            return this.GFI[GaloisField.Mask + gfa - gfb];
        }*/
    }
}
