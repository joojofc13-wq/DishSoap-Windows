using System;
using System.Globalization;
using System.Linq;

namespace Dishwasher
{
    public static class DesktopGameLanguage
    {
        public sealed class Choice
        {
            public string Code { get; }
            public string Name { get; }
            public Choice(string code,string name) { Code=code;Name=name; }
            public override string ToString()=>Name;
        }
        public static readonly Choice[] Choices={
            new("System","Automatic (Windows)"),new("en","English"),
            new("pt-PT","Portuguese (Portugal)"),new("es","Spanish"),new("fr","French"),
            new("de","German"),new("it","Italian"),new("pl","Polish"),new("ru","Russian"),
            new("ja","Japanese"),new("zh-TW","Chinese (Traditional)")
        };
        private static readonly CultureInfo SystemCulture=CultureInfo.CurrentUICulture;
        public static string Normalize(string code)=>Choices.Any(c=>c.Code==code)?code:"System";
        public static CultureInfo Resolve(string code,CultureInfo system=null)
        {
            code=Normalize(code);
            if(code=="System")
            {
                string language=(system??SystemCulture).TwoLetterISOLanguageName;
                code=language=="pt"?"pt-PT":language=="zh"?"zh-TW":Choices.Any(c=>c.Code==language)?language:"en";
            }
            return CultureInfo.GetCultureInfo(code);
        }
        public static void Apply(string code)
        {
            var culture=Resolve(code);
            CultureInfo.CurrentCulture=culture;
            CultureInfo.CurrentUICulture=culture;
            Log.Info("[language] Game culture: "+culture.Name);
        }
    }
}
