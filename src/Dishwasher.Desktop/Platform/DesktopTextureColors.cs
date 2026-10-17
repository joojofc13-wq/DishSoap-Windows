using System;

namespace Dishwasher
{
    public static class DesktopTextureColors
    {
        // BGRA buffers. Keep original channel differences exactly: B > R is
        // an authored bloom mask, not simply a display colour in this game.
        // Neural inference may sharpen luminance, never invent chrominance.
        public static void Preserve(byte[] original, byte[] enhanced)
        {
            if (original.Length != enhanced.Length || original.Length % 4 != 0)
                throw new ArgumentException("Matching BGRA buffers required.");
            for (int i = 0; i < original.Length; i += 4)
            {
                int b = original[i], g = original[i + 1], r = original[i + 2], alpha = original[i + 3];
                int low = Math.Min(r, Math.Min(g, b)), high = Math.Max(r, Math.Max(g, b));
                int sourceLuma = (54 * r + 183 * g + 19 * b + 128) >> 8;
                int enhancedLuma = (54 * enhanced[i + 2] + 183 * enhanced[i + 1] + 19 * enhanced[i] + 128) >> 8;
                int delta = high <= 2 ? 0 : Math.Clamp(enhancedLuma - sourceLuma, -low, 255 - high);
                enhanced[i] = alpha == 0 ? (byte)0 : (byte)(b + delta);
                enhanced[i + 1] = alpha == 0 ? (byte)0 : (byte)(g + delta);
                enhanced[i + 2] = alpha == 0 ? (byte)0 : (byte)(r + delta);
                enhanced[i + 3] = (byte)alpha;
            }
        }
    }
}
