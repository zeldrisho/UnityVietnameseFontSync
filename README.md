# Unity Vietnamese Font Sync

A configurable **BepInEx 5 plugin for Unity Mono games using TextMeshPro**. Add missing Vietnamese glyphs or use a consistent replacement font for selected font groups.

This plugin does not translate text. It can work alongside XUnity.AutoTranslator or an existing Vietnamese translation.

## Status

**Development build; in-game validation is pending.** Automated tests and API-surface checks against two different game assembly sets pass. They do not verify native font generation or rendering.

- Initial runtime target: Windows, BepInEx 5, Unity Mono, compatible TextMeshPro.
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

## Quick configuration

Enable `Diagnostics = true` to log TMP font asset names, or optionally inspect them with UnityExplorer. Match font asset names, not component names.

Example for a fictional `BodyText SDF` asset:

```ini
[Fonts]
Profiles = body

[Font.body]
File = MyVietnameseFont.ttf

[Rules]
Order = body-text
Exclude = Icons, Symbols

[Rule.body-text]
Profile = body
Match = Exact
Pattern = BodyText SDF
Mode = Fallback
```

Place the font in the plugin's `Fonts/` directory, select a filename from Windows Fonts, or specify an absolute path. Use a font you are licensed to use that covers all required characters.

See [configuration](docs/configuration.md) for multiple profiles, exclusions, synchronization, and atlas settings.

**Synchronization clears the matched font's character table in memory.** Use narrow rules and check symbols, untranslated text, and layout. Restart to recover. Matching outlines, shadows, spacing, and line breaks is not guaranteed.

## With XUnity.AutoTranslator

When this plugin owns TMP fonts, start with XUnity's competing font settings empty:

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
| Profile disabled | Log warnings for missing files, API failures, or missing `U+...` characters; try another font or larger atlas |
| Accents look wrong | Fallback cannot replace an existing incorrect glyph; consider narrowly targeted synchronization |
| Mixed styles | Fallback preserves available original glyphs; synchronization changes the whole matched font group |
| Icons/other languages disappear | Avoid broad synchronization and verify preserved fallback coverage |
| Combining marks fail | Add marks to `CustomCharacters`; test the source font and TMP shaping behavior |

To disable, set `Enabled = false` and restart. To uninstall, close the game and remove the plugin DLL.

## Development

See [development](docs/development.md) for build/test commands, runtime design, and release steps. Notable changes are recorded in [CHANGELOG.md](CHANGELOG.md).

CI checks formatting, locked restore, compilation, tests, and packaging logic. A matching version tag publishes the release ZIP after those checks pass.

## License

[MIT](LICENSE). The license covers this plugin and its documentation, not third-party fonts or game assemblies. Do not redistribute proprietary system fonts or game assets.
