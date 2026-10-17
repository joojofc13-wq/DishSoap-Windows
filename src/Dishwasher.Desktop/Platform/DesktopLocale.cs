namespace Dishwasher
{
    // English is the project language, regardless of the Windows region.
    public static class DesktopLocale
    {
        internal static string PreviewLanguage = "en";
        public static string LanguageForRegion(string region, string fallback) => "en";
        public static string Text(string portuguese, string english, string spanish) => english;
    }
}
