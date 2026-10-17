using System.Globalization;
using System.Resources;
using Dishwasher;

internal static class GameLanguageChecks
{
    public static void Run(string root)
    {
        var original=CultureInfo.CurrentCulture;var originalUi=CultureInfo.CurrentUICulture;
        try
        {
            foreach(var choice in DesktopGameLanguage.Choices)
            {
                var settings=DesktopSettings.Load();settings.GameLanguage=choice.Code;settings.Save();
                if(DesktopSettings.Load().GameLanguage!=choice.Code) throw new Exception("Language choice was not preserved.");
            }
            if(DesktopGameLanguage.Resolve("System",CultureInfo.GetCultureInfo("pt-BR")).Name!="pt-PT" ||
               DesktopGameLanguage.Resolve("System",CultureInfo.GetCultureInfo("zh-CN")).Name!="zh-TW" ||
               DesktopGameLanguage.Resolve("System",CultureInfo.GetCultureInfo("ko-KR")).Name!="en" ||
               DesktopGameLanguage.Normalize("unsupported")!="System") throw new Exception("Language fallback failed.");
            var selected=DesktopSettings.Load();selected.GameLanguage="pt-PT";selected.Save();
            DesktopSettings.SaveFullscreen(!selected.Fullscreen);
            if(DesktopSettings.Load().GameLanguage!="pt-PT") throw new Exception("A fullscreen shortcut overwrote the selected game language.");
            string resources=Path.Combine(root,"language-resources");Directory.CreateDirectory(resources);
            foreach(var item in new[]{("","English fixture"),(".pt","Portuguese fixture"),(".ja","Japanese fixture")})
            {
                using var writer=new ResourceWriter(Path.Combine(resources,"Fixture"+item.Item1+".resources"));
                writer.AddResource("test",item.Item2);
            }
            var manager=ResourceManager.CreateFileBasedResourceManager("Fixture",resources,null);
            foreach(var item in new[]{("pt-PT","Portuguese fixture"),("en","English fixture"),("ja","Japanese fixture")})
            {
                DesktopGameLanguage.Apply(item.Item1);
                if(manager.GetString("test")!=item.Item2 || CultureInfo.CurrentCulture.Name!=item.Item1)
                    throw new Exception("Selected game language did not reach resource lookup.");
            }
            manager.ReleaseAllResources();
            Console.WriteLine("PASS game language persistence, supported fonts, region fallback and resource selection");
        }
        finally { CultureInfo.CurrentCulture=original;CultureInfo.CurrentUICulture=originalUi; }
    }
}
