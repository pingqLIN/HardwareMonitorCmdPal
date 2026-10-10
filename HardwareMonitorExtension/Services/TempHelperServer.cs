// Copyright (c) HardwareMonitor. MIT license.

using System.IO.Pipes;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorExtension.Services;

/// <summary>
/// CPU 温度助手服务端（由 SYSTEM 计划任务以 -TempHelper 参数启动本 exe）。
/// SYSTEM 权限可加载 LHM 内核驱动，从而读到 AMD SMN / Intel MSR 的真实 CPU 温度；
/// 通过命名管道 HardwareMonitorTemp 供普通权限的扩展进程读取（16 字节：double 温度 + long ticks），
/// 并落盘 ProgramData 兜底文件，供管道不可用时读取。
/// 修复点：旧版计划任务指向不存在的 h.exe，且全库没有管道服务端，助手链路整体断裂。
/// </summary>
public static class TempHelperServer
{
    public static void Run()
    {
        using var mutex = new Mutex(true, @"Local\HardwareMonitorTempHelper", out var isNew);
        if (!isNew) return; // 已有实例在跑

        // 只开 CPU，最小化 SYSTEM 常驻进程的资源占用
        Computer? computer = null;
        try
        {
            computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = false,
                IsStorageEnabled = false,
                IsMotherboardEnabled = false,
                IsMemoryEnabled = false,
                IsNetworkEnabled = false,
                IsBatteryEnabled = false,
            };
            computer.Open();
        }
        catch
        {
            computer?.Close();
            computer = null;
        }

        try
        {
            RunLoop(computer);
        }
        finally
        {
            computer?.Close();
        }
    }

    private static void RunLoop(Computer? computer)
    {
        var lastTemp = 0d;
        var lastTicks = 0L;

        while (true)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 1) 采样（每轮一次，约等于客户端轮询频率）
            try
            {
                var t = SampleCpuTemp(computer);
                if (t is > 1 and < 150)
                {
                    lastTemp = t.Value;
                    lastTicks = DateTime.UtcNow.Ticks;
                    WriteFallbackFile(lastTemp, lastTicks);
                }
            }
            catch
            {
                // 采样失败保留上次值；兜底文件带时间戳，客户端会校验新鲜度
            }

            // 2) 每轮新建管道实例：避免断连后的 broken 状态导致忙循环
            NamedPipeServerStream? server = null;
            try
            {
                server = new NamedPipeServerStream(
                    ElevatedTempHelper.PipeName,
                    PipeDirection.Out,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                if (server.WaitForConnectionAsync().Wait(TimeSpan.FromSeconds(3)))
                {
                    var buf = new byte[16];
                    BitConverter.GetBytes(lastTemp).CopyTo(buf, 0);
                    BitConverter.GetBytes(lastTicks).CopyTo(buf, 8);
                    server.Write(buf, 0, buf.Length);
                    server.Flush();
                }
            }
            catch
            {
                // 客户端中断/超时：丢弃本轮，继续采样
            }
            finally
            {
                server?.Dispose();
            }

            // 3) 每轮至少 500ms，杜绝任何路径下的忙循环
            var remain = 500 - (int)sw.ElapsedMilliseconds;
            if (remain > 0) Thread.Sleep(remain);
        }
    }

    /// <summary>取 CPU 封装类温度（Tctl/Tdie/Package 优先），全部无效时取最高核心温度。</summary>
    private static double? SampleCpuTemp(Computer? computer)
    {
        if (computer is null) return null;

        var cpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
        if (cpu is null) return null;

        cpu.Update();

        var temps = cpu.Sensors
            .Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue)
            .Select(s => (Name: s.Name, Value: (double)s.Value!.Value))
            .Where(s => s.Value > 0 && s.Value < 150)
            .ToList();

        if (temps.Count == 0) return null;

        var package = temps.FirstOrDefault(s =>
            s.Name.Contains("Tctl", StringComparison.OrdinalIgnoreCase) ||
            s.Name.Contains("Tdie", StringComparison.OrdinalIgnoreCase) ||
            s.Name.Contains("Package", StringComparison.OrdinalIgnoreCase));

        double? best = package.Name is not null ? package.Value : null;
        best ??= temps.Max(s => s.Value);
        return best;
    }

    private static void WriteFallbackFile(double temp, long ticks)
    {
        try
        {
            var dir = @"C:\ProgramData\HardwareMonitorTemp";
            Directory.CreateDirectory(dir);
            var buf = new byte[16];
            BitConverter.GetBytes(temp).CopyTo(buf, 0);
            BitConverter.GetBytes(ticks).CopyTo(buf, 8);
            File.WriteAllBytes(Path.Combine(dir, "cpu-temp.bin"), buf);
        }
        catch
        {
            // 写盘失败不影响管道
        }
    }
}
