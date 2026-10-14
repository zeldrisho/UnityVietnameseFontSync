using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace UnityVietnameseFontSync
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public sealed class UnityVietnameseFontSyncPlugin : BaseUnityPlugin
    {
        private PluginSettings _settings;
        private TmpRuntime _tmp;
        private readonly Dictionary<string, Object> _profiles = new Dictionary<string, Object>();
        private readonly List<Object> _owned = new List<Object>();
        private readonly Dictionary<Object, Snapshot> _patched = new Dictionary<Object, Snapshot>();
        private readonly HashSet<Object> _reported = new HashSet<Object>();
        private readonly Dictionary<Object, float> _retry = new Dictionary<Object, float>();
        private float _nextScan;
        private bool _ready, _assetsInstalled, _apiWarningLogged, _firstFontScanLogged;

        private void Awake()
        {
            Logger.LogInfo("Awake entered; loading configuration.");
            try
            {
                _settings = new PluginSettings(Config);
                Logger.LogInfo("Configuration loaded (Enabled=" + _settings.Enabled + ", profiles=" + _settings.Profiles.Count +
                    ", rules=" + _settings.Rules.Count + ", Diagnostics=" + _settings.Diagnostics + ").");
                InitializeRuntime();
            }
            catch (Exception ex) { Logger.LogError("Invalid configuration; no fonts will be changed: " + ex); }
        }

        private void Start()
        {
            Logger.LogInfo("Start entered.");
            InitializeRuntime();
        }

        private void InitializeRuntime()
        {
            if (_ready) return;
            if (_settings == null)
            {
                Logger.LogError("Configuration unavailable; plugin will not scan fonts.");
                return;
            }
            if (!_settings.Enabled)
            {
                Logger.LogInfo("Plugin is disabled in configuration.");
                return;
            }
            _ready = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger.LogInfo("Runtime scanning initialized.");
            Scan();
        }

        private void Update()
        {
            if (!_ready || _settings.ScanInterval <= 0 || Time.unscaledTime < _nextScan) return;
            Scan();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Scan(); }

        private void Build(FontProfile profile)
        {
            string directory = Path.Combine(Path.GetDirectoryName(Info.Location), "Fonts");
            if (string.IsNullOrWhiteSpace(profile.File)) return;
            var files = new List<string> { profile.File };
            files.AddRange(profile.FallbackFiles);
            int start = _owned.Count;
            try
            {
                Object root = null;
                uint[] remaining = _settings.Characters;
                for (int i = 0; i < files.Count && remaining.Length > 0; i++)
                {
                    string path = FontRules.ResolvePath(files[i], Environment.GetFolderPath(Environment.SpecialFolder.Fonts), directory);
                    if (!File.Exists(path)) throw new FileNotFoundException("Font file not found", path);
                    Font source = new Font(path);
                    Own(source);
                    Object asset = (Object)_tmp.Create(source, profile.Size, profile.Padding, profile.Atlas, profile.MultiAtlas);
                    if (asset == null) throw new InvalidOperationException("TMP font creation returned null.");
                    Own(asset);
                    asset.name = MyPluginInfo.PLUGIN_GUID + "." + profile.Id + (i == 0 ? "" : ".fallback" + i);
                    Own(_tmp.Material(asset) as Object);
                    // Only generated assets are linked. Never link back to game fonts.
                    _tmp.SetFallbacks(asset, _tmp.NewFallbacks());
                    if (root == null) root = asset;
                    else _tmp.Fallbacks(root).Add(asset);
                    bool complete;
                    uint[] missing;
                    try { complete = _tmp.AddCharacters(asset, remaining, out missing); }
                    finally { OwnTextures(asset); }
                    remaining = FontRules.RemainingCharacters(remaining, complete, missing);
                    Logger.LogInfo("Profile '" + profile.Id + "' file '" + files[i] + "': " + remaining.Length + " required characters remain.");
                }
                if (remaining.Length > 0)
                {
                    var codes = new List<string>();
                    foreach (uint code in remaining) codes.Add("U+" + code.ToString("X4"));
                    throw new InvalidOperationException("Required glyphs could not be added: " + string.Join(", ", codes.ToArray()));
                }
                _profiles.Add(profile.Id, root);
                Logger.LogInfo("Loaded profile '" + profile.Id + "' with " + _settings.Characters.Length + " required characters.");
            }
            catch
            {
                for (int i = _owned.Count - 1; i >= start; i--) { Destroy(_owned[i]); _owned.RemoveAt(i); }
                throw;
            }
        }

        private void OwnTextures(Object font)
        {
            IEnumerable textures = _tmp.Textures(font);
            if (textures != null) foreach (object texture in textures) Own(texture as Object);
        }

        private void Own(Object value)
        {
            if (value == null || _owned.Contains(value)) return;
            value.hideFlags |= HideFlags.DontUnloadUnusedAsset;
            _owned.Add(value);
        }

        private void Scan()
        {
            if (!_ready) return;
            _nextScan = Time.unscaledTime + _settings.ScanInterval;
            if (_tmp == null)
            {
                // TMP may be loaded after this plugin starts. Retry discovery, without log spam.
                try
                {
                    _tmp = TmpRuntime.Discover(typeof(Font));
                    Logger.LogInfo("TMP API ready: " + _tmp.FontType.Assembly.GetName().Name + " / " + _tmp.FontType.FullName + ".");
                }
                catch (Exception ex)
                {
                    if (!_apiWarningLogged) Logger.LogWarning("TMP API unavailable; no fonts changed. Discovery will retry: " + ex);
                    _apiWarningLogged = true;
                    return;
                }
                foreach (FontProfile profile in _settings.Profiles)
                {
                    try { Build(profile); }
                    catch (Exception ex) { Logger.LogWarning("Profile '" + profile.Id + "' disabled: " + ex); }
                }
            }
            foreach (Object font in new List<Object>(_patched.Keys)) if (font == null) _patched.Remove(font);
            _reported.RemoveWhere(font => font == null);
            foreach (Object font in new List<Object>(_retry.Keys)) if (font == null) _retry.Remove(font);
            // Pin atlas pages added by TMP since preflight, not just the initial pages.
            foreach (Object asset in new List<Object>(_owned))
                if (asset != null && _tmp.FontType.IsInstanceOfType(asset)) OwnTextures(asset);
            if (_profiles.Count == 0 && !_settings.Diagnostics) return;
            var changed = new HashSet<Object>();
            Object[] fonts = Resources.FindObjectsOfTypeAll(_tmp.FontType);
            if (!_firstFontScanLogged)
            {
                Logger.LogInfo("First TMP font scan found " + fonts.Length + " font assets; profiles ready=" + _profiles.Count +
                    ", configured rules=" + _settings.Rules.Count + ".");
                _firstFontScanLogged = true;
            }
            foreach (Object font in fonts)
            {
                if (font == null || _owned.Contains(font) || _patched.ContainsKey(font)) continue;
                if (_settings.Diagnostics && _reported.Add(font)) Logger.LogInfo("Discovered TMP font: " + font.name);
                if (_retry.TryGetValue(font, out float next) && Time.unscaledTime < next) continue;
                FontRule rule = FontRules.Select(font.name, _settings.Rules, _settings.Exclusions);
                if (rule == null || !_profiles.TryGetValue(rule.Profile, out Object target)) continue;
                Snapshot snapshot = null;
                try
                {
                    snapshot = new Snapshot(_tmp, font, rule.Mode == PatchMode.Synchronize);
                    bool modified = false;
                    IList fallbacks = _tmp.Fallbacks(font);
                    if (fallbacks == null) { fallbacks = _tmp.NewFallbacks(); _tmp.SetFallbacks(font, fallbacks); }
                    // Generated profiles have no edges back to game fonts.
                    if (!fallbacks.Contains(target))
                    {
                        _assetsInstalled = true;
                        if (rule.Mode == PatchMode.Synchronize) fallbacks.Insert(0, target);
                        else fallbacks.Add(target);
                        modified = true;
                    }
                    if (rule.Mode == PatchMode.Synchronize)
                    {
                        if (fallbacks.IndexOf(target) > 0)
                        {
                            fallbacks.Remove(target);
                            fallbacks.Insert(0, target);
                        }
                        _tmp.Freeze(font);
                        _tmp.Characters(font).Clear();
                        _tmp.ReadDefinition(font);
                        modified = true;
                    }
                    _patched.Add(font, snapshot);
                    _retry.Remove(font);
                    if (modified) changed.Add(font);
                    Logger.LogInfo("Applied '" + rule.Id + "' to '" + font.name + "' (" + rule.Mode + ").");
                }
                catch (Exception ex)
                {
                    try { if (snapshot != null) snapshot.Restore(_tmp, font); }
                    catch (Exception restore)
                    {
                        _patched[font] = snapshot;
                        Logger.LogError("Rollback failed; retaining snapshot and generated assets: " + restore);
                    }
                    _retry[font] = Time.unscaledTime + _settings.RetryInterval;
                    Logger.LogWarning("Font patch failed; retry delayed: " + ex);
                }
            }
            if (changed.Count == 0) return;
            foreach (Object value in Resources.FindObjectsOfTypeAll(_tmp.TextType))
            {
                var text = value as Component;
                if (text == null || !text.gameObject.scene.IsValid()) continue;
                try
                {
                    if (changed.Contains(_tmp.TextFont(text) as Object)) _tmp.Refresh(text);
                }
                catch (Exception ex) { Logger.LogWarning("Text refresh failed: " + ex.Message); }
            }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            bool restored = true;
            foreach (var pair in _patched)
            {
                if (pair.Key == null) continue;
                try { pair.Value.Restore(_tmp, pair.Key); }
                catch (Exception ex) { restored = false; Logger.LogError("Restoration failed; retaining generated assets: " + ex); }
            }
            // Used materials can remain referenced by cached submeshes. Restart to reclaim them.
            if (restored && !_assetsInstalled)
            {
                foreach (Object asset in new List<Object>(_owned))
                    if (asset != null && _tmp.FontType.IsInstanceOfType(asset)) OwnTextures(asset);
                for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Destroy(_owned[i]);
            }
        }

        private sealed class Snapshot
        {
            private readonly object _mode;
            private readonly object[] _characters, _fallbacks;
            public Snapshot(TmpRuntime tmp, Object font, bool saveCharacters)
            {
                _mode = tmp.Mode(font);
                _characters = saveCharacters ? Copy(tmp.Characters(font)) : null;
                _fallbacks = Copy(tmp.Fallbacks(font));
            }
            public void Restore(TmpRuntime tmp, Object font)
            {
                tmp.SetMode(font, _mode);
                if (_characters != null)
                {
                    IList characters = tmp.Characters(font);
                    characters.Clear();
                    foreach (object character in _characters) characters.Add(character);
                }
                IList fallbacks = null;
                if (_fallbacks != null)
                {
                    fallbacks = tmp.NewFallbacks();
                    foreach (object fallback in _fallbacks) fallbacks.Add(fallback);
                }
                tmp.SetFallbacks(font, fallbacks);
                tmp.ReadDefinition(font);
            }
            private static object[] Copy(IList values)
            {
                if (values == null) return null;
                var copy = new object[values.Count];
                values.CopyTo(copy, 0);
                return copy;
            }
        }
    }
}
