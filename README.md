# Unity Vietnamese Font Sync

A configurable **BepInEx 5 plugin for Unity Mono games using TextMeshPro**. Add missing Vietnamese glyphs or use a consistent replacement font for selected font groups.

This plugin does not translate text. It can work alongside XUnity.AutoTranslator or an existing Vietnamese translation.

## Compatibility

- Windows, BepInEx 5, Unity Mono, and compatible TextMeshPro APIs.
- No game-specific font mappings or paths are built in.
- BepInEx 6, IL2CPP, legacy Unity UI, NGUI, and UI Toolkit are not supported.
- TMP APIs are discovered at runtime; missing required capabilities disable font changes with a log message. This is not a guarantee of compatibility with every Unity/TMP version.

## Features

- Named font profiles and explicitly ordered matching rules.
- **Fallback:** retain original glyphs and append a Vietnamese-capable font.
- **Synchronize:** opt-in replacement of a matched font group's glyphs.
- Preserve existing fallback chains; leave unmatched fonts unchanged.
- Validate required glyph coverage before applying a profile.
- Discover late-loaded fonts with configurable scanning, retry delays, and diagnostics.
- Load local font files without downloading fonts or using a translation service.

## Installation

1. Install [BepInEx 5 for Unity Mono](https://docs.bepinex.dev/articles/user_guide/installation/index.html).
2. Extract the release archive into `<Game>/BepInEx/`, or [build from source](docs/development.md) and place the DLL at:
   ```text
   <Game>/BepInEx/plugins/zeldrisho.UnityVietnameseFontSync/UnityVietnameseFontSync.dll
   ```
3. Run the game once with the plugin installed to generate:
   ```text
   <Game>/BepInEx/config/zeldrisho.UnityVietnameseFontSync.cfg
   ```
4. Close the game, configure a font file and matching rules, then start it again.

There is no need for a separate BepInEx-only run before installing this plugin. If you previously used a UnityExplorer font script, restart with that script disabled and do not run it alongside the plugin.

The initial configuration makes **no font changes**. No game assemblies or proprietary font files are bundled.

## Worked configuration example

The generated config starts with no matching rules, so it makes no font changes. Close the game before editing the file, then restart it. Enable diagnostics to find the TMP font asset name in `BepInEx/LogOutput.log`; match the asset name, not the text component name.

This complete example synchronizes one fictional asset named `MainMenu SDF`, using a primary font plus a secondary font for missing characters. Replace the filenames and exact asset name with values available in your installation. Put both licensed font files in the plugin's `Fonts/` directory. **Synchronize replaces the matched font's characters in memory**; do not broaden the match to icon or unrelated fonts.

```ini
[General]
Enabled = true
Diagnostics = true
ScanInterval = 2
RetryInterval = 30

[Fonts]
Profiles = menu
CustomCharacters =

[Font.menu]
File = MyVietnameseFont.ttf
FallbackFiles = CoverageFont.ttf
SamplingSize = 36
Padding = 4
AtlasSize = 2048
MultiAtlas = true

[Rules]
Order = menu-text
Exclude = Icons, Symbols

[Rule.menu-text]
Profile = menu
Match = Exact
Pattern = MainMenu SDF
Mode = Synchronize
```

`FallbackFiles` supplies characters missing from the primary file, while keeping its style for available characters. Coverage must be complete across the ordered files; otherwise the profile is disabled. Leave this setting empty when the primary font covers the whole preset.

Keep `Synchronize` when replacing incorrect existing glyphs or making accented and unaccented letters use the same replacement style. Use `Mode = Fallback` instead if you only need missing-character coverage: it retains the original glyphs and cannot replace incorrect ones. Both modes preserve existing fallback chains and leave unmatched fonts unchanged. Restart without the plugin to restore the game's original assets.

See [configuration](docs/configuration.md) for multiple profiles, matching modes, exclusions, and atlas settings.

**Synchronization clears the matched font's character table in memory.** Use narrow rules and check symbols, untranslated text, and layout. Restart to recover. Matching outlines, shadows, spacing, and line breaks is not guaranteed.

## With XUnity.AutoTranslator

If XUnity.AutoTranslator's `FallbackFontTextMeshPro` already renders Vietnamese with acceptable styling, you don't need this plugin. Use it when you need targeted font replacement, consistent accented/unaccented styling, or different replacement fonts for different text groups.

Only when migrating TMP font handling to this plugin, start with XUnity's competing font settings empty:

```ini
[Behaviour]
OverrideFontTextMeshPro=
FallbackFontTextMeshPro=
```

Keep translation enabled. This plugin neither edits XUnity configuration nor rewrites translated strings. Avoid `RemoveAllDiacritics` in translation postprocessing.

## Troubleshooting

| Symptom | Check |
|---|---|
| Nothing changes | `Rules.Order`, exact asset names, `Enabled`, and profile `File`; rules are empty by default |
| Startup logs appear, but no later font discovery | If the game removes or disables BepInEx's manager object, try `[Chainloader] HideManagerGameObject = true` in `BepInEx/config/BepInEx.cfg`, then restart |
| Profile disabled | Log warnings for missing files, API failures, or missing `U+...` characters; try another font or larger atlas |
| Accents look wrong | Fallback cannot replace an existing incorrect glyph; consider narrowly targeted synchronization |
| Mixed styles | Fallback preserves available original glyphs; synchronization changes the whole matched font group |
| Icons/other languages disappear | Avoid broad synchronization and verify preserved fallback coverage |
| Combining marks fail | Add marks to `CustomCharacters`; test the source font and TMP shaping behavior |

`HideManagerGameObject` is a BepInEx setting, not a plugin setting; do not add it to this plugin's config. It hides BepInEx's manager object from scene discovery to help protect it from game cleanup. It affects all plugins on that manager and is not required in every game. Keep it enabled in installations where it restores ongoing scanning.

To disable, set `Enabled = false` and restart. To uninstall, close the game and remove the plugin DLL.

## Development

See [development](docs/development.md) for build/test commands, runtime design, and release steps. Notable changes are recorded in [CHANGELOG.md](CHANGELOG.md).

CI checks formatting, locked restore, compilation, tests, and packaging logic. A matching version tag publishes the release ZIP after those checks pass.

## License

[MIT](LICENSE). The license covers this plugin and its documentation, not third-party fonts or game assemblies. Do not redistribute proprietary system fonts or game assets.
