using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public static class EVVLevelLoader
{
    static readonly Regex FileNamePattern = new Regex(@"^\d{2}-\d{2}(_.*)?\.yml$", RegexOptions.IgnoreCase);

    public static string LevelsDirectory
    {
        get { return Path.Combine(Application.dataPath, "Levels"); }
    }

    public static List<EVVLevelDefinition> DiscoverLevels()
    {
        List<EVVLevelDefinition> levels = new List<EVVLevelDefinition>();
        string directory = LevelsDirectory;
        if (!Directory.Exists(directory))
        {
            Debug.LogWarning($"Levels folder not found: {directory}");
            return levels;
        }

        string[] filePaths = Directory.GetFiles(directory, "*.yml");
        foreach (string filePath in filePaths)
        {
            string fileName = Path.GetFileName(filePath);
            if (!FileNamePattern.IsMatch(fileName))
            {
                Debug.LogWarning($"Skipping level file '{fileName}': name must look like 'NN-NN.yml' or 'NN-NN_name.yml'.");
                continue;
            }

            EVVLevelDefinition level = LoadFromFile(filePath);
            if (level != null)
            {
                levels.Add(level);
            }
        }

        levels.Sort(CompareLevels);
        return levels;
    }

    static int CompareLevels(EVVLevelDefinition a, EVVLevelDefinition b)
    {
        int stageCompare = a.Stage.CompareTo(b.Stage);
        if (stageCompare != 0)
        {
            return stageCompare;
        }

        return a.Level.CompareTo(b.Level);
    }

    public static EVVLevelDefinition LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            Debug.LogWarning($"Level file not found: {path}");
            return null;
        }

        try
        {
            string text = File.ReadAllText(path);
            EVVLevelDefinition level = LoadFromText(text);
            level.SourcePath = path;
            return level;
        }
        catch (Exception exception)
        {
            Debug.LogError($"Failed to load level '{path}': {exception.Message}");
            return null;
        }
    }

    public static EVVLevelDefinition LoadFromText(string yamlText)
    {
        object parsed = EVVMiniYaml.Parse(yamlText);
        Dictionary<string, object> root = parsed as Dictionary<string, object>;
        if (root == null)
        {
            throw new FormatException("Level YAML root must be a mapping.");
        }

        EVVLevelDefinition level = new EVVLevelDefinition();
        level.Id = GetString(root, "id", "");
        level.Name = GetString(root, "name", "");
        level.Stage = GetInt(root, "stage", 1);
        level.Level = GetInt(root, "level", 1);

        object settingsObject;
        if (root.TryGetValue("settings", out settingsObject))
        {
            Dictionary<string, object> settings = settingsObject as Dictionary<string, object>;
            if (settings != null)
            {
                // "lanes" is retained as a legacy alias for "rows" so existing levels keep
                // working while new levels can describe the board in grid terms.
                level.BoardRows = GetInt(settings, "rows", GetInt(settings, "lanes", level.BoardRows));
                level.BoardColumns = GetInt(settings, "columns", level.BoardColumns);
                level.StartingCurrency = GetInt(settings, "starting_currency", level.StartingCurrency);
            }
        }

        object unitsObject;
        if (root.TryGetValue("available_units", out unitsObject))
        {
            List<object> units = unitsObject as List<object>;
            if (units != null)
            {
                foreach (object unit in units)
                {
                    if (unit != null)
                    {
                        level.AvailableUnits.Add(Convert.ToString(unit, CultureInfo.InvariantCulture));
                    }
                }
            }
        }

        object unlocksObject;
        if (root.TryGetValue("unlocks", out unlocksObject))
        {
            List<object> unlocks = unlocksObject as List<object>;
            if (unlocks != null)
            {
                foreach (object unlock in unlocks)
                {
                    if (unlock != null)
                    {
                        level.Unlocks.Add(Convert.ToString(unlock, CultureInfo.InvariantCulture));
                    }
                }
            }
        }

        object wavesObject;
        if (root.TryGetValue("waves", out wavesObject))
        {
            List<object> waves = wavesObject as List<object>;
            if (waves != null)
            {
                foreach (object waveObject in waves)
                {
                    Dictionary<string, object> waveMap = waveObject as Dictionary<string, object>;
                    if (waveMap != null)
                    {
                        level.Waves.Add(ParseWave(waveMap));
                    }
                }
            }
        }

        ResolveWaveTimings(level);

        return level;
    }

    // A wave's start time is either an explicit "time" (legacy, absolute seconds from level
    // start) or a "time_offset" (seconds after the previous wave ends, chained). A wave's
    // duration is implied by its latest spawn's end offset, not authored directly.
    static void ResolveWaveTimings(EVVLevelDefinition level)
    {
        float cursor = 0f;
        foreach (EVVWaveDefinition wave in level.Waves)
        {
            wave.Time = wave.UsesTimeOffset ? cursor + wave.TimeOffset : wave.Time;
            cursor = wave.Time + wave.Duration;
        }
    }

    static EVVWaveDefinition ParseWave(Dictionary<string, object> waveMap)
    {
        EVVWaveDefinition wave = new EVVWaveDefinition();
        wave.Type = ParseWaveType(GetString(waveMap, "tier", GetString(waveMap, "type", "normal")));

        if (waveMap.ContainsKey("time"))
        {
            wave.Time = GetFloat(waveMap, "time", 0f);
            wave.UsesTimeOffset = false;
        }
        else
        {
            wave.TimeOffset = GetFloat(waveMap, "time_offset", 0f);
            wave.UsesTimeOffset = true;
        }

        object spawnsObject;
        if (waveMap.TryGetValue("spawns", out spawnsObject))
        {
            List<object> spawns = spawnsObject as List<object>;
            if (spawns != null)
            {
                foreach (object spawnObject in spawns)
                {
                    Dictionary<string, object> spawnMap = spawnObject as Dictionary<string, object>;
                    if (spawnMap != null)
                    {
                        wave.Spawns.Add(ParseSpawn(spawnMap));
                    }
                }
            }
        }

        float duration = 0f;
        foreach (EVVSpawnDefinition spawn in wave.Spawns)
        {
            duration = Mathf.Max(duration, spawn.EndOffset());
        }

        wave.Duration = duration;
        return wave;
    }

    static EVVSpawnDefinition ParseSpawn(Dictionary<string, object> spawnMap)
    {
        EVVSpawnDefinition spawn = new EVVSpawnDefinition();
        spawn.Unit = GetString(spawnMap, "unit", "");
        spawn.Count = GetInt(spawnMap, "count", 1);
        spawn.Lane = GetString(spawnMap, "lane", "random");
        spawn.Interval = GetFloat(spawnMap, "interval", 1f);
        spawn.TimeOffset = GetFloat(spawnMap, "time_offset", 0f);
        spawn.Duration = GetFloat(spawnMap, "duration", 0f);
        spawn.Spacing = ParseSpacing(GetString(spawnMap, "spacing", "random"));
        return spawn;
    }

    static EVVSpawnSpacing ParseSpacing(string text)
    {
        string normalized = text.Trim().ToLowerInvariant();
        return normalized == "even" ? EVVSpawnSpacing.Even : EVVSpawnSpacing.Random;
    }

    static EVVWaveType ParseWaveType(string text)
    {
        string normalized = text.Trim().ToLowerInvariant();
        if (normalized == "flag")
        {
            return EVVWaveType.Flag;
        }

        if (normalized == "final")
        {
            return EVVWaveType.Final;
        }

        return EVVWaveType.Normal;
    }

    static string GetString(Dictionary<string, object> map, string key, string defaultValue)
    {
        object value;
        if (map.TryGetValue(key, out value) && value != null)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return defaultValue;
    }

    static int GetInt(Dictionary<string, object> map, string key, int defaultValue)
    {
        object value;
        if (!map.TryGetValue(key, out value) || value == null)
        {
            return defaultValue;
        }

        if (value is int)
        {
            return (int)value;
        }

        if (value is float)
        {
            return Mathf.RoundToInt((float)value);
        }

        int parsed;
        if (value is string && int.TryParse((string)value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    static float GetFloat(Dictionary<string, object> map, string key, float defaultValue)
    {
        object value;
        if (!map.TryGetValue(key, out value) || value == null)
        {
            return defaultValue;
        }

        if (value is float)
        {
            return (float)value;
        }

        if (value is int)
        {
            return (int)value;
        }

        float parsed;
        if (value is string && float.TryParse((string)value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        return defaultValue;
    }
}
