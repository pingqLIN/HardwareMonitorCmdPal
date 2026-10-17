using System.Reflection;
using HardwareMonitorExtension;
using HardwareMonitorExtension.Dock;
using HardwareMonitorExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

// Exercise the built extension and real SDK objects without starting hardware sampling.
var directory = Path.Combine(Path.GetTempPath(), "HardwareMonitor-DockSettings-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var path = Path.Combine(directory, "pins.json");
using var hub = new SensorHub();
hub.ConfigurePins(path);
hub.Pinned.Set([]);
var pageType = typeof(SensorHub).Assembly.GetType("HardwareMonitorExtension.Pages.DockSettingsPage", throwOnError: true)!;
using var pageLifetime = (IDisposable)Activator.CreateInstance(pageType, hub)!;
var page = (ListPage)pageLifetime;
var configureItem = new ListItem(page) { Title = "Configure" };
using var band = new PinnedSensorsDockBand(hub, configureItem);
var checks = 0;

void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    checks++;
    Console.WriteLine("PASS " + name);
}

void Publish(params SensorReading[] sensors)
{
    typeof(SensorHub).GetField("_snap", BindingFlags.Instance | BindingFlags.NonPublic)!
        .SetValue(hub, new SensorSnapshot(DateTimeOffset.Now, sensors, false));
    var updated = (EventHandler?)typeof(SensorHub).GetField("SnapshotUpdated", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(hub);
    updated?.Invoke(hub, EventArgs.Empty);
}

void Invoke(IListItem item) => ((IInvokableCommand)item.Command!).Invoke(page);

Check(page.Id == "hardwaremonitor.dock_settings", "configuration page has a stable ID");
Check(band.Items.Length == 1 && ReferenceEquals(band.Items[0], configureItem), "configuration remains available with no pins");
Check(page.GetItems().Length == 1, "empty sampling shows a placeholder");
var cpu = new SensorReading("/cpu/temp", "处理器", "CPU Package", "test", SensorKind.Temperature, 45, "°C", "CPU", "CPU 封装");
var gpu = new SensorReading("/gpu/load", "显卡", "GPU Core", "test", SensorKind.Load, 20, "%", "GPU", "GPU 占用");
Publish(cpu, gpu);
var rows = page.GetItems();
Check(rows.Length == 2, "all sampled sensors can be selected");
var cpuRow = rows.Single(item => item.Title.Contains("CPU", StringComparison.Ordinal));
var beforeName = cpuRow.Command!.Name;
Invoke(cpuRow);
Check(hub.Pinned.IsPinned(cpu.Id), "click pins the selected reading");
Check(new PinnedSensorStore(path).IsPinned(cpu.Id), "selection is persisted immediately");
Check(cpuRow.Command.Name != beforeName && cpuRow.Icon is not null, "selection updates the action label and state");
Check(band.Items.Length == 2 && ReferenceEquals(band.Items[^1], configureItem), "readings band updates and retains configuration");
Publish(cpu with { Value = 63 }, gpu);
Check(ReferenceEquals(cpuRow, page.GetItems().Single(item => item.Title.Contains("CPU", StringComparison.Ordinal))), "sample refresh preserves row identity");
Check(cpuRow.Subtitle.Contains("63", StringComparison.Ordinal), "sample refresh updates the displayed value");
Publish(gpu);
rows = page.GetItems();
var offline = rows.Single(item => item.Title == cpu.Id);
Check(rows.Length == 2, "unavailable pinned sensors remain removable");
Invoke(offline);
Check(!hub.Pinned.IsPinned(cpu.Id) && page.GetItems().Length == 1, "click removes the unavailable pin");
Invoke(page.GetItems()[0]);
Check(hub.Pinned.IsPinned(gpu.Id), "another sensor can be pinned");
var clearItem = (ICommandItem)page.GetItems()[0].MoreCommands[0];
((IInvokableCommand)clearItem.Command!).Invoke(page);
Check(hub.Pinned.Ids.Count == 0 && band.Items.Length == 1, "hide all retains the configuration button");
var reloaded = new PinnedSensorStore(path);
reloaded.EnsureDefaults([cpu, gpu]);
Check(reloaded.Ids.Count == 0, "deliberately empty selection survives restart");
var liveRow = page.GetItems()[0];
var originalSubtitle = liveRow.Subtitle;
pageLifetime.Dispose();
band.Dispose();
Publish(gpu with { Value = 99 });
Check(liveRow.Subtitle == originalSubtitle, "disposed settings page ignores further snapshots");
Check(page.GetItems().Length == 0, "disposed page returns no rows");
Console.WriteLine($"{checks} Dock checks passed. Temporary evidence: {directory}");
