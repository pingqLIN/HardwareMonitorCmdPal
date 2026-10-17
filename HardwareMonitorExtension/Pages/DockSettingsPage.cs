// Copyright (c) HardwareMonitor. MIT license.

using HardwareMonitorExtension.Localization;
using HardwareMonitorExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HardwareMonitorExtension.Pages;

/// <summary>Edits the shared, persisted Dock selection without leaving the palette.</summary>
internal sealed partial class DockSettingsPage : ListPage, IDisposable
{
    internal const string PageId = "hardwaremonitor.dock_settings";
    private readonly SensorHub _hub;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (ListItem Item, SetDockPinCommand Toggle)> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly ListItem _empty;
    private string[] _sensorIds = [];
    private bool _disposed;

    public DockSettingsPage(SensorHub hub)
    {
        _hub = hub;
        Id = PageId;
        Title = Text.Get("DockSettingsTitle");
        Name = Text.Get("DockSettingsOpen");
        PlaceholderText = Text.Get("DockSettingsFilter");
        Icon = new IconInfo("⚙");
        _empty = new ListItem(new NoOpCommand())
        {
            Title = Text.Get("DockSettingsEmpty"),
            Subtitle = Text.Get("DockSettingsEmptyDescription"),
        };
        Commands = [new CommandContextItem(new ClearDockPinsCommand(_hub))];
        _hub.SnapshotUpdated += OnSnapshotUpdated;
        _hub.Pinned.Changed += OnPinsChanged;
    }

    public override IListItem[] GetItems()
    {
        lock (_gate)
        {
            if (_disposed) return [];
            var snapshot = _hub.Snapshot;
            _sensorIds = AvailableIds(snapshot);
            if (_sensorIds.Length == 0) return [_empty];

            var keep = new HashSet<string>(_sensorIds, StringComparer.OrdinalIgnoreCase);
            foreach (var id in _items.Keys.Where(id => !keep.Contains(id)).ToArray())
                _items.Remove(id);

            return _sensorIds.Select(id =>
            {
                if (!_items.TryGetValue(id, out var entry))
                {
                    var toggle = new SetDockPinCommand(_hub, id);
                    var item = new ListItem(toggle);
                    entry = (item, toggle);
                    _items.Add(id, entry);
                }

                ApplyItem(id, entry, snapshot);
                return (IListItem)entry.Item;
            }).ToArray();
        }
    }

    private string[] AvailableIds(SensorSnapshot snapshot) => snapshot.Sensors
        .OrderBy(sensor => sensor.Group, StringComparer.Ordinal)
        .ThenBy(sensor => sensor.Name, StringComparer.Ordinal)
        .ThenBy(sensor => sensor.Id, StringComparer.OrdinalIgnoreCase)
        .Select(sensor => sensor.Id)
        .Concat(_hub.Pinned.Ids)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private void ApplyItem(string id, (ListItem Item, SetDockPinCommand Toggle) entry, SensorSnapshot snapshot)
    {
        var sensor = snapshot.Find(id);
        var pinned = _hub.Pinned.IsPinned(id);
        entry.Toggle.UpdateState(pinned);
        entry.Item.Title = sensor is null ? id : $"{Text.DisplayLabel(sensor.ShortLabel)} · {Text.DisplayName(sensor.Name)}";
        var status = Text.Get(pinned ? "DockSettingsPinned" : "DockSettingsUnpinned");
        entry.Item.Subtitle = sensor is null
            ? $"{status} · {Text.Get("DockSettingsUnavailable")}"
            : $"{status} · {Text.DisplayGroup(sensor.Group)} · {sensor.DisplayValue}";
        entry.Item.Icon = new IconInfo(pinned ? "☑" : "☐");
    }

    private void OnSnapshotUpdated(object? sender, EventArgs e) => Refresh();

    private void OnPinsChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        bool changed;
        lock (_gate)
        {
            if (_disposed) return;
            var snapshot = _hub.Snapshot;
            changed = !_sensorIds.SequenceEqual(AvailableIds(snapshot), StringComparer.OrdinalIgnoreCase);
            if (!changed)
                foreach (var (id, entry) in _items)
                    ApplyItem(id, entry, snapshot);
        }

        // Only collection changes rebuild the page; live values preserve focus and menus.
        if (changed) RaiseItemsChanged();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _hub.SnapshotUpdated -= OnSnapshotUpdated;
            _hub.Pinned.Changed -= OnPinsChanged;
        }
    }
}

internal sealed partial class SetDockPinCommand : InvokableCommand
{
    private readonly SensorHub _hub;
    private readonly string _sensorId;

    public SetDockPinCommand(SensorHub hub, string sensorId)
    {
        _hub = hub;
        _sensorId = sensorId;
        Icon = new IconInfo("📌");
        UpdateState(_hub.Pinned.IsPinned(sensorId));
    }

    public void UpdateState(bool pinned) => Name = Text.Get(pinned ? "DockSettingsUnpin" : "DockSettingsPin");

    public override CommandResult Invoke()
    {
        _hub.Pinned.Toggle(_sensorId);
        return CommandResult.KeepOpen();
    }
}

internal sealed partial class ClearDockPinsCommand : InvokableCommand
{
    private readonly SensorHub _hub;

    public ClearDockPinsCommand(SensorHub hub)
    {
        _hub = hub;
        Name = Text.Get("DockSettingsClear");
        Icon = new IconInfo("☐");
    }

    public override CommandResult Invoke()
    {
        _hub.Pinned.Set([]);
        return CommandResult.KeepOpen();
    }
}
