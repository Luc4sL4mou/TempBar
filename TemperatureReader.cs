using LibreHardwareMonitor.Hardware;

namespace TempBar;

/// <summary>Lê temperaturas de CPU/GPU usando a LibreHardwareMonitor.</summary>
public sealed class TemperatureReader : IDisposable
{
    private readonly Computer? _computer;

    public TemperatureReader()
    {
        try
        {
            _computer = new Computer { IsCpuEnabled = true, IsGpuEnabled = true };
            _computer.Open();
        }
        catch
        {
            _computer = null; // sem admin ou driver bloqueado: Read() devolverá null
        }
    }

    /// <summary>Temperatura em °C do sensor pedido ("CPU" ou "GPU"), ou null se indisponível.</summary>
    public double? Read(string sensor)
    {
        if (_computer == null) return null;
        bool wantGpu = sensor == "GPU";

        try
        {
            foreach (var hw in _computer.Hardware)
            {
                bool isGpu = hw.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
                bool isCpu = hw.HardwareType == HardwareType.Cpu;
                if (wantGpu ? !isGpu : !isCpu) continue;

                var temps = new List<ISensor>();
                Collect(hw, temps);
                var value = Pick(temps, wantGpu);
                if (value != null) return value;
            }
        }
        catch
        {
            // falha momentânea de leitura
        }
        return null;
    }

    private static void Collect(IHardware hw, List<ISensor> into)
    {
        hw.Update();
        into.AddRange(hw.Sensors.Where(s => s.SensorType == SensorType.Temperature && s.Value.HasValue));
        foreach (var sub in hw.SubHardware) Collect(sub, into);
    }

    private static double? Pick(List<ISensor> temps, bool gpu)
    {
        if (temps.Count == 0) return null;

        string[] preferred = gpu
            ? new[] { "GPU Core", "GPU Hot Spot" }
            : new[] { "Core (Tctl/Tdie)", "CPU Package", "Core Max", "Package", "Core (Tdie)", "Tctl" };

        foreach (var name in preferred)
        {
            var hit = temps.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit.Value;
        }

        if (!gpu)
        {
            var cores = temps.Where(s => s.Name.StartsWith("Core", StringComparison.OrdinalIgnoreCase)).ToList();
            if (cores.Count > 0) return cores.Average(s => s.Value!.Value);
        }

        return temps[0].Value;
    }

    public void Dispose() => _computer?.Close();
}
