// Copyright (c) HardwareMonitor. MIT license.

using HardwareMonitorExtension.Dock;
using HardwareMonitorExtension.Localization;
using HardwareMonitorExtension.Pages;
using HardwareMonitorExtension.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HardwareMonitorExtension;

/// <summary>
/// 顶级命令提供程序。
/// </summary>
public partial class HardwareMonitorCommandsProvider : CommandProvider
{
    private readonly SensorHub _hub = new();
    private readonly HardwareMonitorSettingsManager _settingsManager;
    private readonly PinnedSensorsDockBand _dockBand;
    private readonly DockSettingsPage _dockSettingsPage;
    private readonly DockItemAppearanceStore _dockAppearances;
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _bands;

    public HardwareMonitorCommandsProvider()
    {
        DisplayName = Text.Get("AppName");
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _settingsManager = new HardwareMonitorSettingsManager(_hub);
        _hub.ConfigurePins(HardwareMonitorSettingsManager.PinsJsonPath());
        _dockAppearances = new DockItemAppearanceStore(HardwareMonitorSettingsManager.DockAppearanceJsonPath());
        _dockSettingsPage = new DockSettingsPage(_hub, _dockAppearances);
        var configureDockItem = new ListItem(_dockSettingsPage)
        {
            Title = Text.Get("DockSettingsTitle"),
            Subtitle = Text.Get("DockSettingsDescription"),
            Icon = new IconInfo("⚙"),
        };
        _dockBand = new PinnedSensorsDockBand(_hub, configureDockItem, _dockAppearances);

        var listPage = new SensorsListPage(_hub, _settingsManager, _dockAppearances);
        _commands =
        [
            new CommandItem(_dockSettingsPage)
            {
                Title = Text.Get("DockSettingsTitle"),
                Subtitle = Text.Get("DockSettingsDescription"),
                Icon = new IconInfo("⚙"),
            },
            new CommandItem(listPage)
            {
                Title = DisplayName,
                Subtitle = Text.Get("AllSensorsSubtitle"),
                MoreCommands = [
                    new CommandContextItem(new OpenDashboardCommand())
                    {
                        Title = Text.Get("OpenDashboard"),
                        Icon = new IconInfo("🌐"),
                    },
                    new CommandContextItem(new ToggleCompactCommand(listPage)),
                    new CommandContextItem(_settingsManager.Settings.SettingsPage),
                ],
            },
            new CommandItem(new OpenDashboardCommand())
            {
                Title = Text.Get("DashboardTitle"),
                Subtitle = Text.Get("DashboardSubtitle"),
                Icon = new IconInfo("🌐"),
            },
            new CommandItem(new StartElevatedTempHelperCommand())
            {
                Title = Text.Get("InstallHelper"),
                Subtitle = Text.Get("InstallHelperSubtitle"),
                Icon = new IconInfo("🔐"),
            },
        ];

        // Wrap the page as a single button: a bare IListPage would expand every sensor in the Dock.
        _bands = [_dockBand, new WrappedDockItem([configureDockItem],
            "hardwaremonitor.dock_settings_band", Text.Get("DockSettingsTitle"))
        {
            Icon = new IconInfo("⚙"),
        }];

        Settings = _settingsManager.Settings;
        _ = _hub.StartAsync();
    }

    public override ICommandItem[] TopLevelCommands() => _commands;

    public override ICommandItem[] GetDockBands() => _bands;

    public override void Dispose()
    {
        _dockBand.Dispose();
        _dockSettingsPage.Dispose();
        _hub.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }
}
