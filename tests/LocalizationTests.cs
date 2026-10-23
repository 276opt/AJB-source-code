using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccurateJudgementBar;
using Newtonsoft.Json.Linq;

internal static class LocalizationTests
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception("Localization: " + message);
        }
        string directory = Path.Combine(Path.GetTempPath(), "AJB-language-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var errors = new List<string>();
            var loc = new Localization(directory, "{\"Title\":\"English\",\"Missing\":\"Fallback\"}", errors.Add);
            loc.Reload("unknown");
            Check(loc.SelectedLanguage == "en" && loc.Get("Title") == "English", "missing pack fallback");
            File.WriteAllText(Path.Combine(directory, "zh-TW.json"), "{\"$name\":\"繁體中文\",\"Title\":\"測試\",\"Missing\":\"\"}");
            File.WriteAllText(Path.Combine(directory, "bad.json"), "{bad");
            File.WriteAllText(Path.Combine(directory, "number.json"), "{\"Title\":12}");
            File.WriteAllText(Path.Combine(directory, "duplicate.json"), "{\"Title\":\"a\",\"Title\":\"b\"}");
            loc.Reload("ZH-tw");
            Check(loc.SelectedLanguage == "zh-TW" && loc.Get("Title") == "測試", "Unicode and selection");
            Check(loc.Get("Missing") == "Fallback", "empty translation fallback");
            Check(loc.Get("UnknownKey") == "UnknownKey", "unknown key fallback");
            Check(loc.HasLoadErrors && errors.Count == 3, "bad packs isolated and logged");
            Check(loc.LanguageIds.Length == 2 && loc.LanguageNames.Contains("繁體中文 (zh-TW)"), "valid pack list");
            File.WriteAllText(Path.Combine(directory, "zh-TW.json"), "{\"Title\":\"已更新\"}");
            loc.Reload("zh-TW");
            Check(loc.Get("Title") == "已更新" && loc.Get("Missing") == "Fallback", "reload and missing key");
            File.WriteAllText(Path.Combine(directory, "en.json"), "{\"Title\":\"Custom English\"}");
            loc.Reload("en");
            Check(loc.Get("Title") == "Custom English" && loc.Get("Missing") == "Fallback", "English overrides with embedded fallback");
            File.Delete(Path.Combine(directory, "zh-TW.json"));
            loc.Reload("zh-TW");
            Check(loc.SelectedLanguage == "en", "removed selected pack");
            var absent = new Localization(Path.Combine(directory, "absent"), "{\"Title\":\"English\"}", errors.Add);
            absent.Reload("en");
            Check(!absent.HasLoadErrors && absent.Get("Title") == "English", "missing directory");

            Localization.Initialize(AppContext.BaseDirectory, "zh-CN", errors.Add);
            Check(Localization.Current.Get("BarSettings") == "精准判定条设置", "embedded resource and bundled translation");
            var english = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Languages", "en.json")));
            var chinese = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Languages", "zh-CN.json")));
            Check(english.Properties().Select(p => p.Name).OrderBy(p => p)
                .SequenceEqual(chinese.Properties().Select(p => p.Name).OrderBy(p => p)), "bundled key coverage");
            Console.WriteLine("Localization: " + checks + " checks passed.");
        }
        finally
        {
            // Only remove files in the uniquely created test directory.
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
