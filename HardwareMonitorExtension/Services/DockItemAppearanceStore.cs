// Copyright (c) HardwareMonitor. MIT license.
using System.Text.Json;
using System.Globalization;

namespace HardwareMonitorExtension.Services;

/// <summary>Per-sensor Dock display overrides; kept separate from pinned-sensors.json.</summary>
public sealed record DockItemAppearance(string Icon = "", string Title = "", string Subtitle = "");

public sealed class DockItemAppearanceStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private Dictionary<string, DockItemAppearance> _items;

    public event EventHandler? Changed;

    public DockItemAppearanceStore(string path)
    {
        _path = path;
        _items = Load(path);
    }

    public DockItemAppearance Get(string sensorId)
    {
        lock (_gate)
            return _items.TryGetValue(sensorId, out var value) ? value : new();
    }

    public void Set(string sensorId, DockItemAppearance appearance)
    {
        if (string.IsNullOrWhiteSpace(sensorId)) throw new ArgumentException("Sensor ID required", nameof(sensorId));

        var normalized = Normalize(appearance);
        lock (_gate)
        {
            var next = new Dictionary<string, DockItemAppearance>(_items, StringComparer.OrdinalIgnoreCase);
            if (normalized == new DockItemAppearance())
                next.Remove(sensorId);
            else
                next[sensorId] = normalized;

            if (next.Count == _items.Count && next.All(pair =>
                _items.TryGetValue(pair.Key, out var current) && current == pair.Value))
                return;

            // Persist before exposing new in-memory values. A failed write must not pretend to save.
            Save(next);
            _items = next;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reset(string sensorId) => Set(sensorId, new());

    public static DockItemAppearance Normalize(DockItemAppearance raw) => new(
        TrimSingleLine(raw.Icon, 12),
        TrimSingleLine(raw.Title, 32),
        TrimSingleLine(raw.Subtitle, 96));

    private static string TrimSingleLine(string? value, int maxTextElements)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var single = string.Join(" ", value.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))
            .Trim();
        if (single.Length == 0) return "";
        var starts = StringInfo.ParseCombiningCharacters(single);
        return starts.Length <= maxTextElements ? single : single[..starts[maxTextElements]];
    }

    private static Dictionary<string, DockItemAppearance> Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
            var saved = JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
            if (saved?.Version != 1 || saved.Items is null) return new(StringComparer.OrdinalIgnoreCase);
            return saved.Items.Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && entry.Value is not null)
                .ToDictionary(entry => entry.Key, entry => Normalize(entry.Value), StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            // Do not overwrite a malformed/unreadable file on the next edit without a backup.
            throw new InvalidDataException($"Cannot read Dock appearance settings at {path}", ex);
        }
    }

    private void Save(Dictionary<string, DockItemAppearance> items)
    {
        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(directory)) throw new InvalidOperationException("Settings path must have a directory");
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, Path.GetRandomFileName());
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(new Stored(1, items)));
            File.Move(temp, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private sealed record Stored(int Version, Dictionary<string, DockItemAppearance> Items);
}
