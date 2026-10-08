using System.Text.Json;
using System.Text.Json.Nodes;
using AVAFlight.Core.Model;
using AVAFlight.Core.Serialization;

namespace AVAFlight.Infrastructure.Persistence;

/// <summary>Metadata shown in the save-slot list without deserializing the whole game.</summary>
public sealed record SaveSlotInfo(string Slot, string DisplayName, DateTimeOffset SavedAt, string Preset,
    string StarDate, string Location, int SchemaVersion);

/// <summary>Thrown for any save that cannot be loaded; carries a player-facing message.</summary>
public sealed class SaveLoadException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Save files are JSON envelopes: { format, schemaVersion, savedAt, displayName, summary, state }.
/// Writes are atomic (temp file + rename). Older schema versions are migrated step by step; newer
/// versions and corrupt files are rejected with a clear <see cref="SaveLoadException"/>.
/// </summary>
public sealed class SaveStore(AppPaths paths)
{
    public const string FormatTag = "AVAFlight.Save";

    /// <summary>Migrations keyed by the version they upgrade *from*. Each mutates the raw JSON.</summary>
    private static readonly Dictionary<int, Action<JsonObject>> Migrations = new()
    {
        // v1 -> v2: v1 saves predate the captain's log; add an empty one.
        [1] = state => state["log"] ??= new JsonArray(),
    };

    public static string SanitizeSlot(string slot)
    {
        var chars = slot.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        var s = new string(chars);
        return string.IsNullOrEmpty(s) ? "slot" : s[..Math.Min(48, s.Length)];
    }

    private string PathFor(string slot) => Path.Combine(paths.Saves, SanitizeSlot(slot) + ".avasave");

    public void Save(string slot, string displayName, GameState state)
    {
        paths.EnsureCreated();
        var envelope = new JsonObject
        {
            ["format"] = FormatTag,
            ["schemaVersion"] = GameState.CurrentSchemaVersion,
            ["savedAt"] = DateTimeOffset.Now.ToString("O"),
            ["displayName"] = displayName,
            ["preset"] = state.Preset.ToString(),
            ["starDate"] = state.Clock.ToString(),
            ["location"] = state.LocationSummary(),
            ["state"] = JsonSerializer.SerializeToNode(state, GameJson.Options),
        };
        var path = PathFor(slot);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, envelope.ToJsonString(GameJson.Options));
        File.Move(tmp, path, overwrite: true);
    }

    public GameState Load(string slot) => LoadFile(PathFor(slot));

    public static GameState LoadFile(string path)
    {
        if (!File.Exists(path)) throw new SaveLoadException($"Save file not found: {Path.GetFileName(path)}");
        JsonObject root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                   ?? throw new SaveLoadException("Save file is not a JSON object.");
        }
        catch (JsonException e)
        {
            throw new SaveLoadException("Save file is corrupt and cannot be read.", e);
        }
        return FromEnvelope(root);
    }

    public static GameState FromEnvelope(JsonObject root)
    {
        if ((string?)root["format"] != FormatTag)
            throw new SaveLoadException("This file is not an AVAFlight save.");
        int version = (int?)root["schemaVersion"] ?? 0;
        if (version <= 0) throw new SaveLoadException("Save file has no valid schema version.");
        if (version > GameState.CurrentSchemaVersion)
            throw new SaveLoadException(
                $"Save was made by a newer AVAFlight (schema v{version}; this build reads up to v{GameState.CurrentSchemaVersion}).");
        if (root["state"] is not JsonObject state) throw new SaveLoadException("Save file has no game state.");

        while (version < GameState.CurrentSchemaVersion)
        {
            if (!Migrations.TryGetValue(version, out var migrate))
                throw new SaveLoadException($"No migration available from save schema v{version}.");
            migrate(state);
            version++;
        }

        try
        {
            var gs = state.Deserialize<GameState>(GameJson.Options)
                     ?? throw new SaveLoadException("Save file game state is empty.");
            gs.Validate();
            return gs;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException or ArgumentException or NotSupportedException)
        {
            throw new SaveLoadException("Save file is damaged: " + e.Message, e);
        }
    }

    public IReadOnlyList<SaveSlotInfo> List()
    {
        if (!Directory.Exists(paths.Saves)) return [];
        var result = new List<SaveSlotInfo>();
        foreach (var file in Directory.EnumerateFiles(paths.Saves, "*.avasave"))
        {
            try
            {
                if (JsonNode.Parse(File.ReadAllText(file)) is not JsonObject o || (string?)o["format"] != FormatTag)
                {
                    result.Add(new SaveSlotInfo(Path.GetFileNameWithoutExtension(file), "(unreadable save)",
                        File.GetLastWriteTime(file), "?", "?", "?", 0));
                    continue;
                }
                result.Add(new SaveSlotInfo(Path.GetFileNameWithoutExtension(file),
                    (string?)o["displayName"] ?? "?",
                    DateTimeOffset.TryParse((string?)o["savedAt"], out var t) ? t : File.GetLastWriteTime(file),
                    (string?)o["preset"] ?? "?", (string?)o["starDate"] ?? "?", (string?)o["location"] ?? "?",
                    (int?)o["schemaVersion"] ?? 0));
            }
            catch (Exception e) when (e is JsonException or IOException)
            {
                result.Add(new SaveSlotInfo(Path.GetFileNameWithoutExtension(file), "(corrupt save)",
                    File.GetLastWriteTime(file), "?", "?", "?", 0));
            }
        }
        return result.OrderByDescending(s => s.SavedAt).ToList();
    }

    public void Delete(string slot)
    {
        var p = PathFor(slot);
        if (File.Exists(p)) File.Delete(p);
    }
}
