using System.Globalization;
using Microsoft.Win32;

namespace Dishwasher
{
    public static class DesktopLocale
    {
        internal static string PreviewLanguage;
        public static string LanguageForRegion(string region, string fallback) => region?.ToUpperInvariant() switch
        {
            "BR" or "PT" or "AO" or "MZ" or "CV" or "GW" or "ST" or "TL" => "pt",
            "ES" or "MX" or "AR" or "BO" or "CL" or "CO" or "CR" or "CU" or "DO" or
            "EC" or "GQ" or "GT" or "HN" or "NI" or "PA" or "PE" or "PR" or "PY" or "SV" or "UY" or "VE" => "es",
            null or "" => fallback,
            _ => "en"
        };
        private static string WindowsLanguage()
        {
            try
            {
                using var geo = Registry.CurrentUser.OpenSubKey(@"Control Panel\International\Geo");
                return LanguageForRegion(geo?.GetValue("Name") as string, CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
            }
            catch { return CultureInfo.CurrentCulture.TwoLetterISOLanguageName; }
        }
        // Windows country/region takes precedence over regional format and UI language.
        public static string Text(string portuguese, string english, string spanish) =>
            (PreviewLanguage ?? WindowsLanguage()) switch
            {
                "pt" => portuguese,
                "es" => spanish,
                _ => english
            };
    }
}
