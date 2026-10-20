using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;
using System.Text;

namespace TempBar;

/// <summary>
/// Início automático com o Windows. Como o app exige administrador, uma entrada em "Run" abriria o UAC
/// a cada logon; uma tarefa agendada com privilégios elevados inicia sem perguntar.
/// </summary>
public static class Autostart
{
    public const string Argument = "--autostart";
    private const string TaskName = "TempBar";

    public static bool IsEnabled() => RunSchtasks($"/Query /TN \"{TaskName}\"");

    public static bool Enable()
    {
        string exe = Environment.ProcessPath ?? "";
        if (exe.Length == 0) return false;

        string xml = $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>{SecurityElement.Escape(WindowsIdentity.GetCurrent().Name)}</UserId>
                  <Delay>PT5S</Delay>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>{SecurityElement.Escape(WindowsIdentity.GetCurrent().Name)}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>{SecurityElement.Escape(exe)}</Command>
                  <Arguments>{Argument}</Arguments>
                </Exec>
              </Actions>
            </Task>
            """;

        string file = Path.Combine(Path.GetTempPath(), $"TempBar-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, xml, Encoding.Unicode);   // o agendador espera UTF-16
            return RunSchtasks($"/Create /TN \"{TaskName}\" /XML \"{file}\" /F");
        }
        finally
        {
            try { File.Delete(file); } catch { }
        }
    }

    public static bool Disable() => RunSchtasks($"/Delete /TN \"{TaskName}\" /F");

    private static bool RunSchtasks(string args)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (process == null) return false;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
