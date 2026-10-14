using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Loader;
using UnityVietnameseFontSync;
using Xunit;

public sealed class TmpRuntimeTests
{
    /// <summary>
    /// Verifies reflected font creation, glyph population, atlas freezing, fallback assignment, and text refresh.
    /// </summary>
    [Fact]
    public void AdapterValidatesAndInvokesCompatibleApi()
    {
        var api = new TmpRuntime(typeof(TestFont), typeof(TestText), typeof(TestSource));
        var font = (TestFont)api.Create(new TestSource(), 36, 4, 2048, true);
        Assert.Equal(TestPopulation.Dynamic, api.Mode(font));
        Assert.True(api.AddCharacters(font, new uint[] { 65 }, out uint[] missing));
        Assert.Empty(missing);
        Assert.Single(api.Characters(font));
        api.Freeze(font);
        Assert.Equal(TestPopulation.Static, api.Mode(font));
        var fallback = api.NewFallbacks();
        fallback.Add(new TestFont());
        api.SetFallbacks(font, fallback);
        Assert.Single(api.Fallbacks(font));
        var text = new TestText { font = font };
        Assert.Same(font, api.TextFont(text));
        api.Refresh(text);
        Assert.True(text.Refreshed);
    }

    /// <summary>
    /// Verifies that the adapter returns failed glyph population and its missing codes.
    /// </summary>
    [Fact]
    public void AdapterReportsFailedGlyphPreflight()
    {
        var api = new TmpRuntime(typeof(TestFont), typeof(TestText), typeof(TestSource));
        Assert.False(api.AddCharacters(new TestFont(), new uint[] { 0xFFFF }, out uint[] missing));
        Assert.Equal(new uint[] { 0xFFFF }, missing);
    }

    /// <summary>
    /// Verifies that incompatible font or text types are rejected during adapter construction.
    /// </summary>
    [Fact]
    public void MissingCapabilitiesAreRejectedBeforeMutation()
    {
        Assert.Throws<NotSupportedException>(() => new TmpRuntime(typeof(object), typeof(TestText), typeof(TestSource)));
        Assert.Throws<NotSupportedException>(() => new TmpRuntime(typeof(TestFont), typeof(object), typeof(TestSource)));
    }

    /// <summary>
    /// Validates the configured external Unity and TMP assembly API in an isolated load context without rendering.
    /// </summary>
    [GameApiFact]
    public void ExternalGameApiHasRequiredCapabilities()
    {
        string directory = Path.GetFullPath(Environment.GetEnvironmentVariable("GAME_MANAGED_PATH"));
        var context = new AssemblyLoadContext("game-api-check", isCollectible: true);
        context.Resolving += (loader, name) =>
        {
            string path = Path.Combine(directory, name.Name + ".dll");
            return File.Exists(path) && name.Name.StartsWith("Unity", StringComparison.Ordinal)
                ? loader.LoadFromAssemblyPath(path) : null;
        };
        try
        {
            var tmp = context.LoadFromAssemblyPath(Path.Combine(directory, "Unity.TextMeshPro.dll"));
            var rendering = context.LoadFromAssemblyPath(Path.Combine(directory, "UnityEngine.TextRenderingModule.dll"));
            var api = new TmpRuntime(tmp.GetType("TMPro.TMP_FontAsset", true), tmp.GetType("TMPro.TMP_Text", true),
                rendering.GetType("UnityEngine.Font", true));
            Assert.NotNull(api.FontType);
        }
        finally { context.Unload(); }
    }

    public enum TestPopulation { Static, Dynamic }
    public enum TestRender { SDFAA }
    public sealed class TestSource { }
    public sealed class TestFont
    {
        public List<uint> characterTable { get; } = new List<uint>();
        public List<TestFont> fallbackFontAssetTable { get; set; }
        public TestPopulation atlasPopulationMode { get; set; }
        public object[] atlasTextures { get; } = new object[0];
        public object material { get; } = new object();
        /// <summary>
        /// Creates a fake font with the requested population mode and an optional trailing parameter for adapter validation.
        /// </summary>
        public static TestFont CreateFontAsset(TestSource source, int size, int padding, TestRender render,
            int width, int height, TestPopulation mode, bool multiAtlas, bool optional = false)
            => new TestFont { atlasPopulationMode = mode };
        /// <summary>
        /// Simulates population failure for U+FFFF; otherwise appends the requested codes and reports complete coverage.
        /// </summary>
        public bool TryAddCharacters(uint[] characters, out uint[] missing, bool includeFeatures)
        {
            if (Array.IndexOf(characters, 0xFFFFu) >= 0) { missing = new uint[] { 0xFFFF }; return false; }
            characterTable.AddRange(characters);
            missing = new uint[0];
            return true;
        }
        /// <summary>
        /// Provides the no-op definition rebuild capability required by the adapter fixture.
        /// </summary>
        public void ReadFontAssetDefinition() { }
    }
    public sealed class TestText
    {
        public TestFont font { get; set; }
        public bool Refreshed;
        /// <summary>
        /// Provides the no-op dirty notification capability required by the adapter fixture.
        /// </summary>
        public void SetAllDirty() { }
        /// <summary>
        /// Records that the adapter requested a mesh refresh.
        /// </summary>
        public void ForceMeshUpdate(bool ignoreActive, bool reparse) { Refreshed = true; }
    }

    public sealed class GameApiFactAttribute : FactAttribute
    {
        /// <summary>
        /// Skips external API validation when GAME_MANAGED_PATH is not configured.
        /// </summary>
        public GameApiFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GAME_MANAGED_PATH")))
                Skip = "Set GAME_MANAGED_PATH for a local API-surface check; this is not a rendering test.";
        }
    }
}
