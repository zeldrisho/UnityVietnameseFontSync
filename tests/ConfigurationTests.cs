using System;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using UnityVietnameseFontSync;
using Xunit;

public sealed class ConfigurationTests
{
    /// <summary>
    /// Verifies rule order, case-insensitive exact matches, exclusions, and the absence of matches.
    /// </summary>
    [Fact]
    public void RulesUseFirstMatchAndExclusionsWin()
    {
        var exact = new FontRule { Pattern = "Body", Match = MatchKind.Exact };
        var all = new FontRule { Match = MatchKind.All };
        Assert.Same(exact, FontRules.Select("bOdY", new[] { exact, all }, new string[0]));
        Assert.Same(all, FontRules.Select("Body Large", new[] { exact, all }, new string[0]));
        Assert.Null(FontRules.Select("Icon Body", new[] { all }, new[] { "icon" }));
        Assert.Null(FontRules.Select("Body", new FontRule[0], new string[0]));
    }

    /// <summary>
    /// Verifies case-insensitive fragment matching and rejection of empty patterns.
    /// </summary>
    [Fact]
    public void ContainsIsCaseInsensitiveAndEmptyNeverMatches()
    {
        Assert.True(new FontRule { Match = MatchKind.Contains, Pattern = "body" }.Matches("BODY SDF"));
        Assert.False(new FontRule { Match = MatchKind.Contains, Pattern = "" }.Matches("BODY"));
    }

    /// <summary>
    /// Verifies Vietnamese coverage, custom Unicode scalars, deduplication, and rejection of unpaired surrogates.
    /// </summary>
    [Fact]
    public void CharacterPresetIncludesVietnameseUpperLowerAndScalars()
    {
        uint[] codes = FontRules.Characters("😀\u0301");
        foreach (char c in "ăâêôơưđĂÂÊÔƠƯĐàáảãạằắẳẵặầấẩẫậèéẻẽẹềếểễệìíỉĩịòóỏõọồốổỗộờớởỡợùúủũụừứửữựỳýỷỹỵ")
        {
            Assert.Contains((uint)c, codes);
            Assert.Contains((uint)char.ToUpperInvariant(c), codes);
        }
        Assert.Contains(0x1F600u, codes);
        Assert.Contains(0x0301u, codes);
        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.Throws<ArgumentException>(() => FontRules.Characters("\uD800"));
    }

    /// <summary>
    /// Verifies custom-directory precedence, system-directory fallback, absolute paths, and empty input.
    /// </summary>
    [Fact]
    public void RelativePathsPreferCustomFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        try
        {
            string file = Path.Combine(directory, "font.ttf");
            File.WriteAllText(file, "test");
            Assert.Equal(file, FontRules.ResolvePath("font.ttf", "/os-fonts", directory));
            Assert.Equal(Path.Combine("/os-fonts", "other.ttf"), FontRules.ResolvePath("other.ttf", "/os-fonts", directory));
            Assert.Equal(file, FontRules.ResolvePath(file, "/os-fonts", directory));
            Assert.Null(FontRules.ResolvePath("", "/os-fonts", directory));
        }
        finally { Directory.Delete(directory, true); }
    }

    /// <summary>
    /// Verifies that list parsing trims entries and preserves their first occurrence.
    /// </summary>
    [Fact]
    public void ListParsingTrimsAndDeduplicates()
    {
        Assert.Equal(new[] { "body", "menu" }, FontRules.Split(" body, ,menu,body "));
    }

    /// <summary>
    /// Verifies that default settings contain no rules and no enabled primary font file.
    /// </summary>
    [Fact]
    public void DefaultsNeverMutateFonts()
    {
        var settings = Load("");
        Assert.Empty(settings.Rules);
        Assert.Equal("", settings.Profiles[0].File);
    }

    /// <summary>
    /// Verifies that invalid IDs, atlas sizes, profile references, and empty rule patterns are rejected.
    /// </summary>
    [Theory]
    [InlineData("[Fonts]\nProfiles = invalid.id\n")]
    [InlineData("[Fonts]\nProfiles = default\n[Font.default]\nAtlasSize = 513\n")]
    [InlineData("[Rules]\nOrder = body\n[Rule.body]\nProfile = missing\nPattern = Body\n")]
    [InlineData("[Rules]\nOrder = body\n[Rule.body]\nPattern = \n")]
    public void InvalidConfigurationIsRejected(string text)
    {
        Assert.Throws<ArgumentException>(() => Load(text));
    }

    /// <summary>
    /// Verifies that configured rule order and synchronization mode are retained.
    /// </summary>
    [Fact]
    public void ConfigurationPreservesExplicitOrder()
    {
        var settings = Load("[Rules]\nOrder = specific, remaining\n[Rule.specific]\nPattern = Body\n[Rule.remaining]\nMatch = All\nMode = Synchronize\n");
        Assert.Equal(new[] { "specific", "remaining" }, settings.Rules.Select(rule => rule.Id));
        Assert.Equal(PatchMode.Synchronize, settings.Rules[1].Mode);
    }

    /// <summary>
    /// Verifies that fallback files default to empty and preserve configured order after deduplication.
    /// </summary>
    [Fact]
    public void ProfileFallbackFilesPreserveOrderAndDefaultToEmpty()
    {
        Assert.Empty(Load("").Profiles[0].FallbackFiles);
        var settings = Load("[Font.default]\nFile = Primary.ttf\nFallbackFiles = Secondary.ttf, Third.ttf, Secondary.ttf\n");
        Assert.Equal("Primary.ttf", settings.Profiles[0].File);
        Assert.Equal(new[] { "Secondary.ttf", "Third.ttf" }, settings.Profiles[0].FallbackFiles);
    }

    /// <summary>
    /// Verifies that incomplete coverage persists until success and invalid missing-character reports are rejected.
    /// </summary>
    [Fact]
    public void CoverageChainRequiresAllRemainingCharacters()
    {
        uint[] requested = { 65, 0x1EF7, 0x1EF6 };
        uint[] remaining = FontRules.RemainingCharacters(requested, false, new uint[] { 0x1EF7 });
        Assert.Equal(new uint[] { 0x1EF7 }, remaining);
        Assert.Equal(remaining, FontRules.RemainingCharacters(remaining, false, remaining));
        Assert.Empty(FontRules.RemainingCharacters(remaining, true, new uint[0]));
        Assert.Throws<InvalidOperationException>(() => FontRules.RemainingCharacters(requested, false, null));
        Assert.Throws<InvalidOperationException>(() => FontRules.RemainingCharacters(requested, false, new uint[0]));
        Assert.Throws<InvalidOperationException>(() => FontRules.RemainingCharacters(requested, false, new uint[] { 999 }));
    }

    /// <summary>
    /// Loads settings from a temporary configuration file with automatic saving disabled, then deletes the file.
    /// </summary>
    private static PluginSettings Load(string text)
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".cfg");
        File.WriteAllText(file, text);
        try { return new PluginSettings(new ConfigFile(file, false) { SaveOnConfigSet = false }); }
        finally { File.Delete(file); }
    }
}
