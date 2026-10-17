using System.Diagnostics;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            string root = AppContext.BaseDirectory;
            string script = Path.Combine(root, "tools", "windows", "owner-builder.ps1");
            if (!File.Exists(script)) throw new FileNotFoundException("Builder files are missing. Extract the entire ZIP before running DishSoap.Builder.exe.", script);
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
            var start = new ProcessStartInfo(powershell) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root };
            foreach (string value in new[] { "-NoLogo", "-NoProfile", "-STA", "-ExecutionPolicy", "Bypass", "-File", script }) start.ArgumentList.Add(value);
            foreach (string value in args) start.ArgumentList.Add(value);
            using var child = Process.Start(start) ?? throw new IOException("Could not start the builder.");
            child.WaitForExit();
            return child.ExitCode;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "DishSoap Builder", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
