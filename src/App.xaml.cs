using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace MilovanaEosEditor;

public partial class App : Application
{
    // ATTACH_PARENT_PROCESS: attach to the console of the process that launched us (e.g. the shell
    // running `dotnet run`), so a WinExe can still write its --generate-map report to that terminal.
    private const int AttachParentProcess = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Headless step-6 invocation: `MilovanaEosEditor.exe --generate-map <teaseDir>`.
        // Runs without ever creating a window so Claude can regenerate asset-map.json from a shell.
        int flag = Array.IndexOf(e.Args, "--generate-map");
        if (flag >= 0)
        {
            RunHeadlessGenerateMap(flag + 1 < e.Args.Length ? e.Args[flag + 1] : null);
            return;
        }

        new MainWindow().Show();
    }

    private void RunHeadlessGenerateMap(string? teaseDir)
    {
        // If stdout is already redirected (a pipe/file, e.g. `dotnet run ... | cat`), Console writes
        // there directly — attaching a console would instead steal it. Only attach to the launching
        // terminal's console when output is NOT redirected (the double-click-from-cmd case).
        if (!Console.IsOutputRedirected)
            AttachConsole(AttachParentProcess); // best-effort; harmless if there's no parent console

        int exitCode;
        try
        {
            if (string.IsNullOrWhiteSpace(teaseDir))
                throw new ArgumentException("Usage: MilovanaEosEditor --generate-map <teaseDir>");

            GenerateResult result = AssetMapGenerator.Run(teaseDir);
            Console.WriteLine(AssetMapGenerator.FormatReport(result));
            exitCode = result.Ok ? 0 : 1; // non-zero when any manifest entry failed to resolve
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"generate-map failed: {ex.Message}");
            exitCode = 2;
        }

        Shutdown(exitCode);
    }
}
