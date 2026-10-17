using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Dishwasher
{
    public static class AtomicSave
    {
        public static void Recover(string path, XmlSerializer serializer)
        {
            if (!File.Exists(path)) return;
            bool Valid(string candidate)
            {
                try { using var stream = File.OpenRead(candidate); serializer.Deserialize(stream); return true; }
                catch (InvalidOperationException ex) { Log.Exception("[save] Invalid XML " + candidate, ex); return false; }
            }
            if (Valid(path)) return;
            string backup = path + ".bak";
            if (!File.Exists(backup) || !Valid(backup))
                throw new InvalidDataException("Corrupt save with no valid backup: " + path);
            string corrupt = path + ".corrupt-" + DateTime.UtcNow.Ticks;
            string temporary = path + ".recover-" + Guid.NewGuid().ToString("N");
            File.Copy(path, corrupt);
            try
            {
                File.Copy(backup, temporary);
                File.Replace(temporary, path, null);
                Log.Info("[save] Backup restored; original preserved: " + corrupt);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        // Called around the original game's XmlSerializer and Save object.
        public static void Write(string path, XmlSerializer serializer, object save)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    serializer.Serialize(stream, save);
                    stream.Flush(true);
                }
                // Validate before committing; a failed write preserves old progress.
                using (var stream = File.OpenRead(temporary)) serializer.Deserialize(stream);
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
                Log.Info("[save] committed " + Path.GetFileName(path));
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
