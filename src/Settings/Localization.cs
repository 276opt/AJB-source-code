using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AccurateJudgementBar
{
    /// <summary>Flat JSON language packs, identified by their filename without extension.</summary>
    public sealed class Localization
    {
        public const string DefaultLanguage = "en";
        public static Localization Current { get; private set; } = null!;

        private readonly string directory;
        private readonly Action<string> log;
        private readonly Dictionary<string, string> fallback;
        private Dictionary<string, Dictionary<string, string>> packs =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public string SelectedLanguage { get; private set; } = DefaultLanguage;
        public string[] LanguageIds { get; private set; } = Array.Empty<string>();
        public string[] LanguageNames => LanguageIds.Select(id =>
            packs[id].TryGetValue("$name", out var name) ? name + " (" + id + ")" : id).ToArray();
        public bool HasLoadErrors { get; private set; }

        public static void Initialize(string modPath, string language, Action<string> log)
        {
            using var stream = typeof(Localization).Assembly.GetManifestResourceStream(
                "AccurateJudgementBar.Languages.en.json")
                ?? throw new InvalidOperationException("Embedded English language pack is missing.");
            using var reader = new StreamReader(stream);
            Current = new Localization(Path.Combine(modPath, "Languages"), reader.ReadToEnd(), log);
            Current.Reload(language);
        }

        public Localization(string directory, string fallbackJson, Action<string> log)
        {
            this.directory = directory;
            this.log = log;
            fallback = Parse(fallbackJson);
        }

        private static Dictionary<string, string> Parse(string json)
        {
            var root = JObject.Parse(json, new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in root.Properties())
            {
                if (property.Value.Type != JTokenType.String)
                    throw new FormatException("Translation '" + property.Name + "' must be a string.");
                string value = (string)property.Value!;
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(property.Name, value);
            }
            return values;
        }

        public void Reload(string language)
        {
            var loaded = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                [DefaultLanguage] = fallback
            };
            HasLoadErrors = false;
            try
            {
                if (Directory.Exists(directory))
                {
                    foreach (string file in Directory.GetFiles(directory, "*.json").OrderBy(p => p, StringComparer.Ordinal))
                    {
                        try
                        {
                            loaded[Path.GetFileNameWithoutExtension(file)] = Parse(File.ReadAllText(file));
                        }
                        catch (Exception ex)
                        {
                            HasLoadErrors = true;
                            log("Cannot load language file '" + file + "': " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HasLoadErrors = true;
                log("Cannot read language directory: " + ex.Message);
            }
            packs = loaded;
            LanguageIds = packs.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
            Select(language);
        }

        public void Select(string language)
        {
            SelectedLanguage = LanguageIds.FirstOrDefault(id =>
                string.Equals(id, language, StringComparison.OrdinalIgnoreCase)) ?? DefaultLanguage;
        }

        public string Get(string key)
        {
            if (packs.TryGetValue(SelectedLanguage, out var selected) && selected.TryGetValue(key, out var value))
                return value;
            return fallback.TryGetValue(key, out var defaultValue) ? defaultValue : key;
        }
    }
}
