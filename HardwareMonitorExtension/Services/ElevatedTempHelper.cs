// Copyright (c) HardwareMonitor. MIT license.

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace HardwareMonitorExtension.Services;

/// <summary>
/// CPU Tctl 数据源：优先命名管道（SYSTEM 助手 TempHelperServer，零写盘）；
/// 失败则读 ProgramData 兜底文件。计划任务的注册/重启也在这里。
/// </summary>
public static class ElevatedTempHelper
{
    public const string TaskName = "HardwareMonitorTempHelper";
    public const string PipeName = "HardwareMonitorTemp";
    private static DateTimeOffset _lastRestart = DateTimeOffset.MinValue;

    public static bool IsTaskInstalled()
    {
        try
        {
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/query /tn {TaskName}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            });
            proc?.WaitForExit(3000);
            return proc?.ExitCode == 0;
        }
        catch { return false; }
    }

    public static bool EnsureAutoStart()
    {
        var script = Path.Combine(Path.GetTempPath(), "hwmon-install-temp-task.ps1");
        // 修复点：旧版指向设置目录下不存在的 h.exe（全库无该产物、无管道服务端）。
        // 现改为复用包内主 exe 的 -TempHelper 模式（见 TempHelperServer）。
        var exe = Path.Combine(AppContext.BaseDirectory, "HardwareMonitorExtension.exe").Replace("'", "''");
        File.WriteAllText(script, $"""
            $action = New-ScheduledTaskAction -Execute '{exe}' -Argument '-TempHelper'
            $trigger = New-ScheduledTaskTrigger -AtStartup
            $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -RunLevel Highest
            $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -RestartCount 5 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero)
            Register-ScheduledTask -TaskName '{TaskName}' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
            Start-ScheduledTask -TaskName '{TaskName}' | Out-Null
            """);
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{script}\"",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            return true;
        }
        catch { return false; }
    }

    public static void TryRestartHelper()
    {
        if (DateTimeOffset.UtcNow - _lastRestart < TimeSpan.FromSeconds(30))
            return;
        _lastRestart = DateTimeOffset.UtcNow;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/run /tn {TaskName}",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
        }
        catch { }
    }

    /// <summary>命名管道读 16 字节：double 温度 + long ticks；超时/失败再读文件。</summary>
    public static double? TryReadCpuTemperature()
    {
        var t = ReadFromPipe();
        if (t is > 1 and < 150) return t;
        return ReadFromFile();
    }

    private static double? ReadFromPipe()
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.In, PipeOptions.Asynchronous);
            client.Connect(300);
            var buf = new byte[16];
            var n = 0;
            while (n < buf.Length)
            {
                var r = client.Read(buf, n, buf.Length - n);
                if (r <= 0) break;
                n += r;
            }

            if (n >= 8)
            {
                var temp = BitConverter.ToDouble(buf, 0);
                if (!double.IsFinite(temp) || temp <= 1 || temp >= 150)
                    return null;
                // 带时间戳时校验新鲜度（服务端每轮采样后应答）
                if (n >= 16)
                {
                    var ticks = BitConverter.ToInt64(buf, 8);
                    if (ticks <= 0 || DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) > TimeSpan.FromMinutes(5))
                        return null;
                }
                return temp;
            }
        }
        catch
        {
            TryRestartHelper();
        }

        return null;
    }

    private static double? ReadFromFile()
    {
        try
        {
            var file = @"C:\ProgramData\HardwareMonitorTemp\cpu-temp.bin";
            if (!File.Exists(file)) return null;
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length < 16) return null;
            var t = BitConverter.ToDouble(bytes, 0);
            var ticks = BitConverter.ToInt64(bytes, 8);
            if (!double.IsFinite(t) || t < 1 || t > 150 || ticks <= 0) return null;
            if (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc) > TimeSpan.FromMinutes(5))
                return null;
            return t;
        }
        catch
        {
            return null;
        }
    }
}
