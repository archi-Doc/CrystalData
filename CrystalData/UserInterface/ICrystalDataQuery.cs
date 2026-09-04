// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.UserInterface;

/// <summary>
/// Defines user decisions requested while loading or recovering crystal data.
/// </summary>
public interface ICrystalDataQuery
{
    Task<AbortOrContinue> NoCheckFile();

    Task<AbortOrContinue> InconsistentJournal(string path);

    Task<AbortOrContinue> FailedToLoad(FileConfiguration configuration, CrystalResult result);

    Task<YesOrNo> LoadBackup(string path);
}
