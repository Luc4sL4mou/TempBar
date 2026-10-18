using System.IO;
using System.Text.Json;

namespace TempBar;

/// <summary>Configurações salvas em settings.json (em %AppData%\TempBar).</summary>
public class Settings
{
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool Locked { get; set; }
    public bool SideBySide { get; set; }                 // false = empilhadas, true = lado a lado
    public double OpacityPercent { get; set; } = 100;   // 20..100
    public double MinTemp { get; set; } = 30;     // extremo esquerdo da barra
    public double MaxTemp { get; set; } = 90;     // extremo direito da barra

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TempBar", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch
        {
            // arquivo corrompido: volta ao padrão
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // sem permissão de escrita: ignora
        }
    }
}
