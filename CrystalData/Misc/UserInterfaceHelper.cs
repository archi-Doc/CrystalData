// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.UserInterface;

public enum YesOrNo
{
    Invalid,
    Yes,
    No,
}

public enum YesOrNoOrYesToAll
{
    Invalid,
    Yes,
    No,
    YesToAll,
}

internal static class UserInterfaceHelper
{
    public static AbortOrContinue ToAbortOrContinue(this YesOrNo yesOrNo)
        => yesOrNo == YesOrNo.Yes ? AbortOrContinue.Continue : AbortOrContinue.Abort;

    public static AbortOrContinue ToAbortOrContinue(this YesOrNoOrYesToAll yesOrNo)
        => (yesOrNo == YesOrNoOrYesToAll.Yes || yesOrNo == YesOrNoOrYesToAll.YesToAll) ? AbortOrContinue.Continue : AbortOrContinue.Abort;

    public static AbortOrContinue ToAbortOrContinue(this bool? yesOrNo)
        => yesOrNo == true ? AbortOrContinue.Continue : AbortOrContinue.Abort;

    /// <summary>
    /// Removes control characters and leading white-space and trailing white-space.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The result.</returns>
    public static string CleanupInput(this string input)
    {
        var span = input.AsSpan();
        Span<char> dest = stackalloc char[input.Length];

        // Remove control characters.
        var destLength = 0;
        foreach (var x in span)
        {
            if (!char.IsControl(x))
            {
                dest[destLength++] = x;
            }
        }

        // Leading white-space
        var start = 0;
        for (start = 0; start < destLength; start++)
        {
            if (!char.IsWhiteSpace(dest[start]))
            {
                break;
            }
        }

        // Trailing white-space
        var end = destLength - 1;
        for (; start < end; end--)
        {
            if (!char.IsWhiteSpace(dest[end]))
            {
                break;
            }
        }

        if (start == 0 && end == (input.Length - 1))
        {// Returns the original string.
            return input;
        }
        else
        {// Cleaned string.
            return dest.Slice(start, end - start + 1).ToString();
        }
    }
}
