using System.Collections.Generic;

public enum EVVWaveType
{
    Normal,
    Flag,
    Final
}

public enum EVVSpawnSpacing
{
    Even,
    Random
}

public class EVVSpawnDefinition
{
    public string Unit = "";
    public int Count = 1;
    public string Lane = "random";

    // Legacy field: fixed seconds between consecutive spawns, used only when Duration <= 0.
    public float Interval = 1f;

    // New scheduling model: spawn starts TimeOffset seconds after its wave starts, and its
    // Count units are distributed across Duration seconds according to Spacing.
    public float TimeOffset = 0f;
    public float Duration = 0f;
    public EVVSpawnSpacing Spacing = EVVSpawnSpacing.Random;

    public float ImpliedDuration()
    {
        if (Duration > 0f)
        {
            return Duration;
        }

        return Count > 1 ? (Count - 1) * Interval : 0f;
    }

    public float EndOffset()
    {
        return TimeOffset + ImpliedDuration();
    }
}

public class EVVWaveDefinition
{
    // Absolute seconds from level start. Computed by EVVLevelLoader from either an explicit
    // legacy "time" value, or by chaining TimeOffset onto the previous wave's end time.
    public float Time;

    // Relative seconds after the previous wave ends (or after level start for the first wave).
    // Only used when the wave YAML specifies "time_offset" instead of an absolute "time".
    public float TimeOffset;
    public bool UsesTimeOffset;

    // Seconds this wave spans, implied by its latest spawn's end offset. Computed by the loader.
    public float Duration;

    public EVVWaveType Type = EVVWaveType.Normal;
    public List<EVVSpawnDefinition> Spawns = new List<EVVSpawnDefinition>();
}

public class EVVLevelDefinition
{
    public string Id = "";
    public string Name = "";
    public int Stage = 1;
    public int Level = 1;
    public int BoardRows = 6;
    public int BoardColumns = 10;
    public int StartingCurrency = 100;
    public List<string> AvailableUnits = new List<string>();
    public List<string> Unlocks = new List<string>();
    public List<EVVWaveDefinition> Waves = new List<EVVWaveDefinition>();
    public string SourcePath = "";
}
