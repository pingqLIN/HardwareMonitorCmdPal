using HardwareMonitorExtension.Localization;
// Copyright (c) HardwareMonitor. MIT license.

using HardwareMonitorExtension.Pages;
using HardwareMonitorExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HardwareMonitorExtension.Dock;

/// <summary>
/// 快捷栏固定读数。
/// 性能：不再每秒生成 PNG 火花线；仅原地改 Title/Icon 字符串。
/// 布局：Title 只含数值（固定短宽），Subtitle 为名称，避免关键读数被截断。
/// </summary>
public sealed partial class PinnedSensorsDockBand : WrappedDockItem, IDisposable
{
    private readonly SensorHub _hub;
    private readonly DockItemAppearanceStore _appearances;
    private readonly IListItem _configureItem;
    private readonly Lock _updateLock = new();
    private readonly Dictionary<string, ListItem> _items = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;
    private bool _hooked;

    public PinnedSensorsDockBand(SensorHub hub, IListItem configureItem, DockItemAppearanceStore appearances)
        : base([], "hardwaremonitor.pinned_band", Text.Get("DockTitle"))
    {
        _hub = hub;
        _appearances = appearances;
        _configureItem = configureItem;
        Icon = new IconInfo("🌡");
        EnsureHubHook();
        _hub.Pinned.Changed += OnPinsChanged;
        _appearances.Changed += OnAppearanceChanged;
        RebuildItems();
    }

    private void EnsureHubHook()
    {
        if (_hooked) return;
        _hooked = true;
        _hub.SnapshotUpdated += OnSnapshotUpdated;
    }

    private void OnPinsChanged(object? sender, EventArgs e) => RebuildItems();

    private void OnAppearanceChanged(object? sender, EventArgs e) => RebuildItems();

    private void OnSnapshotUpdated(object? sender, EventArgs e) => ApplyFromSnapshot(_hub.Snapshot);

    private void RebuildItems()
    {
        lock (_updateLock)
        {
            if (_disposed || _hub.Pinned is null) return;
            var snap = _hub.Snapshot;
            var list = new List<ListItem>();
            foreach (var id in _hub.Pinned.Ids)
            {
                if (!_items.TryGetValue(id, out var item))
                {
                    item = new ListItem(new SensorChartPage(_hub, id))
                    {
                        MoreCommands =
                        [
                            new CommandContextItem(new DockAppearancePage(_appearances, id))
                            {
                                Title = Text.Get("DockAppearanceEdit"),
                                Icon = new IconInfo("🎨"),
                            },
                        ],
                    };
                    _items[id] = item;
                }

                ApplyItem(item, id, snap);
                list.Add(item);
            }

            var keep = new HashSet<string>(_hub.Pinned.Ids, StringComparer.OrdinalIgnoreCase);
            foreach (var key in _items.Keys.Where(k => !keep.Contains(k)).ToList())
                _items.Remove(key);

            // Keep configuration reachable even after the last reading is hidden.
            Items = [.. list, _configureItem];
        }
    }

    private void ApplyFromSnapshot(SensorSnapshot snap)
    {
        lock (_updateLock)
        {
            if (_disposed) return;
            foreach (var (id, item) in _items)
                ApplyItem(item, id, snap);
        }
    }

    private void ApplyItem(ListItem item, string id, SensorSnapshot snap)
    {
        var sensor = snap.Find(id);
        var appearance = _appearances.Get(id);
        if (sensor is null)
        {
            item.Title = string.IsNullOrEmpty(appearance.Title) ? $"{id} --" : $"{appearance.Title} --";
            item.Subtitle = string.IsNullOrEmpty(appearance.Subtitle) ? Text.Get("NoData") : appearance.Subtitle;
            item.Icon = string.IsNullOrEmpty(appearance.Icon) ? new IconInfo("🌡") : new IconInfo(appearance.Icon);
            return;
        }

        // Compact hides Subtitle: identify each reading in the primary label.
        // Custom title labels precede (and never replace) the current numeric value.
        var value = sensor.FormatDockTitle(_hub.MergeLoadInDock);
        item.Title = string.IsNullOrEmpty(appearance.Title)
            ? sensor.FormatDockCompactTitle(_hub.MergeLoadInDock)
            : $"{appearance.Title} {value}";
        item.Subtitle = string.IsNullOrEmpty(appearance.Subtitle)
            ? $"{Text.DisplayLabel(sensor.ShortLabel)} · {Text.DisplayName(sensor.Name)}"
            : appearance.Subtitle;
        item.Icon = string.IsNullOrEmpty(appearance.Icon) ? IconFor(sensor) : new IconInfo(appearance.Icon);
    }

    private static IconInfo IconFor(SensorReading sensor)
    {
        if (sensor.Kind != SensorKind.Temperature)
            return new IconInfo("📈");
        var level = ThresholdPolicy.Classify(sensor.Value);
        return level switch
        {
            HeatLevel.Critical => new IconInfo("🔥"),
            HeatLevel.Hot => new IconInfo("⚠"),
            _ => new IconInfo("🌡"),
        };
    }

    public void Dispose()
    {
        lock (_updateLock)
        {
            if (_disposed) return;
            _disposed = true;
            _hub.Pinned.Changed -= OnPinsChanged;
            _hub.SnapshotUpdated -= OnSnapshotUpdated;
            _appearances.Changed -= OnAppearanceChanged;
        }
    }
}
