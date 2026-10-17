using System;
using System.IO;
using System.Text;

namespace Dishwasher
{
    public static class DesktopPaths
    {
        public static string Root { get; private set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DishSoapWindows");
        public static string Saves => Path.Combine(Root, "Saves");
        public static string Config => Path.Combine(Root, "Config");
        public static string Cache => Path.Combine(Root, "Cache");
        public static string Logs => Path.Combine(Root, "Logs");
        public static string Imports => Path.Combine(Root, "Imports");
        public static string ActiveFile => Path.Combine(Config, "active-content.txt");
        public static void Initialize(string root = null)
        {
            if (root != null) Root = Path.GetFullPath(root);
            foreach (var dir in new[] { Root, Saves, Config, Cache, Logs, Imports })
                Directory.CreateDirectory(dir);
        }
        public static string Under(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':'))
                throw new InvalidDataException("Caminho relativo inválido: " + relative);
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(prefix, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Caminho fora da pasta: " + relative);
            return result;
        }
        public static void AtomicWrite(string path, string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(text);
                    stream.Write(bytes);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
    public static class Log
    {
        private static readonly object Gate = new object();
        public static string FilePath { get; private set; }
        public static void Initialize()
        {
            FilePath = Path.Combine(DesktopPaths.Logs, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff") + ".log");
            Info("DishSoap Windows " + Environment.Version + " " + Environment.OSVersion);
        }
        public static void Info(string message) => Write("INFO", message);
        public static void Error(string message) => Write("ERROR", message);
        public static void Exception(string where, Exception error) => Write("ERROR", where + ": " + error);
        private static void Write(string level, string message)
        {
            string line = DateTime.UtcNow.ToString("O") + " [" + level + "] " + message;
            lock (Gate)
            {
                Console.Error.WriteLine(line);
                if (FilePath != null) File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }
    }
    // The shared content manager and XACT shim need this API, without Android.
    public static class PublicBuild
    {
        public const bool IsPublic = true;
        public static string ContentRoot { get; set; }
        public static Stream OpenContent(string path)
        {
            if (ContentRoot == null) throw new InvalidOperationException("Conteúdo não inicializado.");
            return File.OpenRead(DesktopPaths.Under(ContentRoot, path));
        }
    }
}
