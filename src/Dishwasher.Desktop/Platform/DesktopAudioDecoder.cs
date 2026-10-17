using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Dishwasher.Import
{
    // Same interface used by the upstream AudioImporter. No ARM64 P/Invoke.
    internal static class NativeXma
    {
        public static string FfmpegPath { get; set; } = Environment.GetEnvironmentVariable("DISHWASHER_FFMPEG") ?? "ffmpeg.exe";
        public static bool Available { get; private set; }
        public static string ProbeError { get; private set; }
        public static int Version => Available ? 1 : 0;
        public static bool Probe()
        {
            try
            {
                string version = Run("-version");
                string decoders = Run("-hide_banner", "-decoders");
                string encoders = Run("-hide_banner", "-encoders");
                if (!decoders.Contains("xma2") || !encoders.Contains("libvorbis"))
                    throw new NotSupportedException("FFmpeg precisa do decoder xma2 e encoder libvorbis.");
                Log.Info("[audio] FFmpeg externo: " + version);
                ProbeError = null;
                return Available = true;
            }
            catch (Exception ex)
            {
                ProbeError = ex.Message;
                Log.Exception("[audio] FFmpeg indisponível; PCM ainda pode ser convertido", ex);
                return Available = false;
            }
        }
        internal static string Run(params string[] args)
        {
            var start = new ProcessStartInfo(FfmpegPath)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new IOException("Não foi possível iniciar FFmpeg.");
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120000))
            {
                process.Kill(true);
                process.WaitForExit();
                throw new TimeoutException("FFmpeg excedeu 120 segundos.");
            }
            Task.WaitAll(output, error);
            if (process.ExitCode != 0) throw new InvalidDataException("FFmpeg " + process.ExitCode + ": " + error.Result);
            return output.Result + error.Result;
        }
        private static void Validate(byte[] data, int channels, int rate)
        {
            if (channels < 1 || channels > 2 || rate < 1000 || rate > 192000 || data.Length == 0)
                throw new InvalidDataException("Formato de áudio inválido ou não suportado (mono/estéreo).");
        }
        // RIFF WAVEFORMATEX tag 0x166 with 34-byte XMA2 extradata. Matches the
        // upstream bridge's block geometry, now passed through FFmpeg's WAV demuxer.
        internal static byte[] WrapXma2(byte[] data, int channels, int rate, long samples)
        {
            Validate(data, channels, rate);
            if (data.Length % 2048 != 0 || samples <= 0 || samples > uint.MaxValue)
                throw new InvalidDataException("XMA2 inválido: tamanho de pacote/amostras.");
            const int block = 0x10000;
            int blocks = checked((data.Length + block - 1) / block);
            if (blocks > ushort.MaxValue) throw new InvalidDataException("XMA2 grande demais.");
            using var stream = new MemoryStream();
            using var w = new BinaryWriter(stream, Encoding.ASCII, true);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(checked(72 + data.Length));
            w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(52);
            w.Write((ushort)0x166); w.Write((ushort)channels); w.Write(rate);
            w.Write(0); w.Write((ushort)2048); w.Write((ushort)16); w.Write((ushort)34);
            w.Write((ushort)((channels + 1) / 2)); w.Write(channels == 1 ? 4u : 3u);
            w.Write((uint)samples); w.Write(block); w.Write(0u); w.Write((uint)samples);
            w.Write(0u); w.Write(0u); w.Write((byte)0); w.Write((byte)4); w.Write((ushort)blocks);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(data.Length); w.Write(data);
            w.Flush(); return stream.ToArray();
        }
        public static int XmaToFile(byte[] data, int channels, int rate, long samples, string outPath, int outFmt)
        {
            string input = Path.Combine(DesktopPaths.Cache, Guid.NewGuid().ToString("N") + ".xma.wav");
            string temporary = outPath + ".tmp";
            try
            {
                File.WriteAllBytes(input, WrapXma2(data, channels, rate, samples));
                string format = outFmt == 1 ? "ogg" : "wav";
                string codec = outFmt == 1 ? "libvorbis" : "pcm_s16le";
                string diagnostic = Run("-nostdin", "-hide_banner", "-loglevel", "error", "-xerror", "-y", "-i", input,
                    "-map", "0:a:0", "-ar", Math.Clamp(rate, 8000, 48000).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    "-c:a", codec, "-f", format, temporary);
                if (!string.IsNullOrWhiteSpace(diagnostic)) Log.Info("[audio] " + diagnostic);
                if (!File.Exists(temporary) || new FileInfo(temporary).Length <= 44)
                    throw new InvalidDataException("FFmpeg não produziu áudio.");
                File.Move(temporary, outPath, true);
                return 0;
            }
            catch (Exception ex) { ProbeError = ex.Message; Log.Exception("[audio] XMA " + outPath, ex); return -100; }
            finally
            {
                if (File.Exists(input)) File.Delete(input);
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        public static int Pcm16BeToWav(byte[] data, int channels, int rate, string outPath)
        {
            try
            {
                Validate(data, channels, rate);
                if (data.Length % (channels * 2) != 0) throw new InvalidDataException("PCM truncado.");
                // The upstream importer requests .ogg for music-bank entries,
                // including hypothetical PCM entries in a different revision.
                bool ogg = outPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase);
                string wavPath = ogg ? outPath + ".pcm.wav" : outPath + ".tmp";
                try
                {
                    using (var w = new BinaryWriter(File.Create(wavPath)))
                    {
                        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + data.Length);
                        w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16);
                        w.Write((ushort)1); w.Write((ushort)channels); w.Write(rate);
                        w.Write(rate * channels * 2); w.Write((ushort)(channels * 2)); w.Write((ushort)16);
                        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(data.Length);
                        for (int i = 0; i < data.Length; i += 2) { w.Write(data[i + 1]); w.Write(data[i]); }
                    }
                    if (ogg) Run("-nostdin", "-v", "error", "-y", "-i", wavPath, "-c:a", "libvorbis", "-q:a", "4", outPath);
                    else File.Move(wavPath, outPath, true);
                }
                finally { if (File.Exists(wavPath)) File.Delete(wavPath); }
                return 0;
            }
            catch (Exception ex) { ProbeError = ex.Message; Log.Exception("[audio] PCM " + outPath, ex); return -101; }
        }
    }
}
