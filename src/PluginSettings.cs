using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace UnityVietnameseFontSync
{
    internal sealed class FontProfile
    {
        public string Id, File;
        public string[] FallbackFiles;
        public int Size, Padding, Atlas;
        public bool MultiAtlas;
    }

    internal sealed class PluginSettings
    {
        public readonly List<FontProfile> Profiles = new List<FontProfile>();
        public readonly List<FontRule> Rules = new List<FontRule>();
        public string[] Exclusions;
        public uint[] Characters;
        public float ScanInterval, RetryInterval;
        public bool Enabled, Diagnostics;

        /// <summary>
        /// Binds configuration defaults and loads ordered profiles and rules, rejecting invalid IDs, atlas sizes, modes, and rule references.
        /// </summary>
        public PluginSettings(ConfigFile config)
        {
            Enabled = config.Bind("General", "Enabled", true, "Enable plugin. Restart after configuration changes.").Value;
            Diagnostics = config.Bind("General", "Diagnostics", false, "Log discovered font names once per live asset.").Value;
            ScanInterval = config.Bind("General", "ScanInterval", 2f, new ConfigDescription("Seconds between discovery scans; 0 disables periodic scans.", new AcceptableValueRange<float>(0f, 300f))).Value;
            RetryInterval = config.Bind("General", "RetryInterval", 30f, new ConfigDescription("Seconds before retrying a failed font mutation.", new AcceptableValueRange<float>(1f, 600f))).Value;
            Exclusions = FontRules.Split(config.Bind("Rules", "Exclude", "", "Comma-separated asset-name fragments excluded before all rules; include your game's icon fonts here.").Value);
            Characters = FontRules.Characters(config.Bind("Fonts", "CustomCharacters", "", "Extra characters in addition to ASCII and precomposed Vietnamese. No translated text is normalized.").Value);
            foreach (string id in FontRules.Split(config.Bind("Fonts", "Profiles", "default", "Comma-separated profile IDs.").Value))
            {
                ValidateId(id);
                string section = "Font." + id;
                Profiles.Add(new FontProfile
                {
                    Id = id,
                    File = config.Bind(section, "File", "", "Font filename or absolute path. Empty disables this profile. Relative paths search plugin Fonts then Windows Fonts.").Value,
                    FallbackFiles = FontRules.Split(config.Bind(section, "FallbackFiles", "", "Ordered comma-separated font files used for glyphs missing from File. Full combined coverage is required.").Value),
                    Size = config.Bind(section, "SamplingSize", 36, new ConfigDescription("Font sampling size.", new AcceptableValueRange<int>(8, 128))).Value,
                    Padding = config.Bind(section, "Padding", 4, new ConfigDescription("SDF padding.", new AcceptableValueRange<int>(1, 32))).Value,
                    Atlas = config.Bind(section, "AtlasSize", 2048, "Square atlas size: 256, 512, 1024, 2048, or 4096.").Value,
                    MultiAtlas = config.Bind(section, "MultiAtlas", true, "Allow additional atlas textures.").Value
                });
                int atlas = Profiles[Profiles.Count - 1].Atlas;
                if (atlas < 256 || atlas > 4096 || (atlas & (atlas - 1)) != 0) throw new ArgumentException(section + ": invalid AtlasSize.");
            }
            foreach (string id in FontRules.Split(config.Bind("Rules", "Order", "", "Comma-separated rule IDs in first-match order. Empty means no mutation.").Value))
            {
                ValidateId(id);
                string section = "Rule." + id;
                var rule = new FontRule
                {
                    Id = id,
                    Profile = config.Bind(section, "Profile", "default", "Profile ID to use.").Value,
                    Pattern = config.Bind(section, "Pattern", "", "Case-insensitive font asset name or fragment.").Value,
                    Match = config.Bind(section, "Match", MatchKind.Exact, "Exact, Contains, or All. All explicitly matches every non-excluded asset.").Value,
                    Mode = config.Bind(section, "Mode", PatchMode.Fallback, "Fallback preserves glyphs; Synchronize clears all original characters and freezes the original atlas. Opt-in only.").Value
                };
                if (!Enum.IsDefined(typeof(MatchKind), rule.Match) || !Enum.IsDefined(typeof(PatchMode), rule.Mode)) throw new ArgumentException(section + ": invalid mode.");
                if (!Profiles.Exists(p => p.Id == rule.Profile)) throw new ArgumentException(section + ": unknown profile " + rule.Profile);
                if (rule.Match != MatchKind.All && string.IsNullOrWhiteSpace(rule.Pattern)) throw new ArgumentException(section + ": Pattern must not be empty.");
                Rules.Add(rule);
            }
        }

        /// <summary>
        /// Rejects profile or rule IDs containing characters other than letters, digits, underscores, or hyphens.
        /// </summary>
        private static void ValidateId(string id)
        {
            foreach (char c in id)
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') throw new ArgumentException("Profile/rule IDs must contain only letters, digits, underscores or hyphens: " + id);
        }
    }
}
