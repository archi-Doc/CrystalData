// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.UserInterface;

public interface ICrystalDataQuery
{
    Task<AbortOrContinue> NoCheckFile();

    Task<AbortOrContinue> InconsistentJournal(string path);

    Task<AbortOrContinue> FailedToLoad(FileConfiguration configuration, CrystalResult result);

    Task<YesOrNo> LoadBackup(string path);
}
