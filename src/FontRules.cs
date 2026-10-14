using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityVietnameseFontSync
{
    public enum MatchKind { Exact, Contains, All }
    public enum PatchMode { Fallback, Synchronize }

    public sealed class FontRule
    {
        public string Id;
        public string Profile;
        public string Pattern;
        public MatchKind Match;
        public PatchMode Mode;
        public bool Matches(string name)
        {
            if (Match == MatchKind.All) return true;
            if (string.IsNullOrEmpty(Pattern)) return false;
            return Match == MatchKind.Exact
                ? string.Equals(name, Pattern, StringComparison.OrdinalIgnoreCase)
                : name.IndexOf(Pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public static class FontRules
    {
        public static string[] Split(string value)
        {
            var result = new List<string>();
            foreach (string part in (value ?? "").Split(','))
                if (part.Trim().Length > 0 && !result.Contains(part.Trim())) result.Add(part.Trim());
            return result.ToArray();
        }

        public static FontRule Select(string name, IEnumerable<FontRule> rules, string[] exclusions)
        {
            foreach (string exclusion in exclusions)
                if (name.IndexOf(exclusion, StringComparison.OrdinalIgnoreCase) >= 0) return null;
            foreach (FontRule rule in rules) if (rule.Matches(name)) return rule;
            return null;
        }

        public static string ResolvePath(string file, string fontsDirectory, string customDirectory)
        {
            if (string.IsNullOrWhiteSpace(file)) return null;
            if (Path.IsPathRooted(file)) return file;
            string custom = Path.Combine(customDirectory, file);
            if (File.Exists(custom)) return custom;
            return Path.Combine(fontsDirectory, file);
        }

        public static uint[] Characters(string extra)
        {
            var text = new StringBuilder();
            for (int i = 32; i <= 126; i++) text.Append((char)i);
            const string vowels = "aăâeêioôơuưyAĂÂEÊIOÔƠUƯY";
            const string tones = "\u0300\u0301\u0303\u0309\u0323";
            text.Append("đĐ").Append(vowels);
            foreach (char vowel in vowels)
                foreach (char tone in tones)
                    text.Append((vowel.ToString() + tone).Normalize(NormalizationForm.FormC));
            // Combining marks are optional custom characters: source fonts may not support them.
            text.Append(extra ?? "");
            var result = new HashSet<uint>();
            string value = text.ToString();
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                    result.Add((uint)char.ConvertToUtf32(value[i], value[++i]));
                else if (!char.IsSurrogate(value[i])) result.Add(value[i]);
                else throw new ArgumentException("Custom characters contain an unpaired surrogate.");
            }
            var codes = new uint[result.Count];
            result.CopyTo(codes);
            Array.Sort(codes);
            return codes;
        }
    }
}
