using System;
using System.IO;

namespace Dishwasher
{
    public static class DesktopSaveBackup
    {
        // Returns null when there is no save yet. Backups live outside Saves so
        // a later backup cannot recursively include all earlier backups.
        public static string Create()
        {
            if (!Directory.Exists(DesktopPaths.Saves)) return null;
            var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
            string[] files = Directory.GetFiles(DesktopPaths.Saves, "*", options);
            if (files.Length == 0) return null;
            string folder = Path.Combine(DesktopPaths.Root, "SaveBackups", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            foreach (string source in files)
            {
                string target = Path.Combine(folder, Path.GetRelativePath(DesktopPaths.Saves, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target, false);
            }
            return folder;
        }
    }
}
