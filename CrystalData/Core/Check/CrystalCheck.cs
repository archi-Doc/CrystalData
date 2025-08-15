// Copyright (c) All contributors. All rights reserved. Licensed under the MIT license.

namespace CrystalData.Check;

internal class CrystalCheck
{
    private static readonly SaveFormat Format = SaveFormat.Binary;

    public CrystalCheck(ILogger<CrystalCheck> logger)
    {
        this.logger = logger;
    }

    public void RegisterDataAndConfiguration(DataAndConfigurationIdentifier identifier, out bool newlyRegistered)
    {
        newlyRegistered = this.data.DataAndConfigurations.TryAdd(identifier, 0);
    }

    public void ClearShortcutPosition()
        => this.data.WaypointToShortcutPosition.Clear();

    public void SetShortcutPosition(Waypoint waypoint, ulong position)
        => this.data.WaypointToShortcutPosition[waypoint] = position;

    public bool TryGetPlanePosition(Waypoint waypoint, out ulong position)
        => this.data.WaypointToShortcutPosition.TryGetValue(waypoint, out position);

    public void Load(string filePath)
    {
        try
        {
            this.filePath = filePath;
            var bytes = File.ReadAllBytes(filePath);

            var result = SerializeHelper.TryDeserialize<CrystalCheckData>(bytes, Format, false, default);
            if (result.Data != null)
            {
                this.data = result.Data;
                this.SuccessfullyLoaded = true;
            }
        }
        catch
        {
            this.logger.TryGet(LogLevel.Error)?.Log($"Could not load the check file: {this.filePath}");
        }
    }

    public void Store()
    {
        /*if (!this.SuccessfullyLoaded)
        {
            return;
        }*/

        try
        {
            byte[] b;
            if (Format == SaveFormat.Binary)
            {
                b = TinyhandSerializer.Serialize(this.data);
            }
            else
            {
                b = TinyhandSerializer.SerializeToUtf8(this.data);
            }

            File.WriteAllBytes(this.filePath, b);
        }
        catch
        {
            this.logger.TryGet(LogLevel.Error)?.Log($"Could not write the check file: {this.filePath}");
        }
    }

    public bool SuccessfullyLoaded { get; internal set; } = false;

    public string FilePath => this.filePath;

    private ILogger logger;
    private string filePath = string.Empty;
    private CrystalCheckData data = TinyhandSerializer.Reconstruct<CrystalCheckData>();
}
