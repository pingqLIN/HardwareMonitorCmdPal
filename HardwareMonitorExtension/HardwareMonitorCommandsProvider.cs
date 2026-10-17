// Copyright (c) HardwareMonitor. MIT license.

using HardwareMonitorExtension.Dock;
using HardwareMonitorExtension.Localization;
using HardwareMonitorExtension.Pages;
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
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _bands;

    public HardwareMonitorCommandsProvider()
    {
        DisplayName = "硬件监视器";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        _settingsManager = new HardwareMonitorSettingsManager(_hub);
        _hub.ConfigurePins(HardwareMonitorSettingsManager.PinsJsonPath());
        _dockSettingsPage = new DockSettingsPage(_hub);
        var configureDockItem = new ListItem(_dockSettingsPage)
        {
            Title = Text.Get("DockSettingsTitle"),
            Subtitle = Text.Get("DockSettingsDescription"),
            Icon = new IconInfo("⚙"),
        };
        _dockBand = new PinnedSensorsDockBand(_hub, configureDockItem);

        var listPage = new SensorsListPage(_hub, _settingsManager);
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
                Subtitle = "全部传感器 · 可固定到快捷栏",
                MoreCommands = [
                    new CommandContextItem(new OpenDashboardCommand())
                    {
                        Title = "打开实时图表看板",
                        Icon = new IconInfo("🌐"),
                    },
                    new CommandContextItem(new ToggleCompactCommand(listPage)),
                    new CommandContextItem(_settingsManager.Settings.SettingsPage),
                ],
            },
            new CommandItem(new OpenDashboardCommand())
            {
                Title = "实时图表看板",
                Subtitle = "btop 风格离线 index.html",
                Icon = new IconInfo("🌐"),
            },
            new CommandItem(new StartElevatedTempHelperCommand())
            {
                Title = "安装 CPU 温度开机采样（一次）",
                Subtitle = "注册后开机自动，无需再点；普通权限 CPU Tctl 为 0",
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
