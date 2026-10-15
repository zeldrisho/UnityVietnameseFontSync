# Unity Vietnamese Font Sync

A configurable **BepInEx 5 plugin for Unity Mono games using TextMeshPro**. Add missing Vietnamese glyphs or use a consistent replacement font for selected font groups.

This plugin does not translate text. It can work alongside [XUnity.AutoTranslator](https://github.com/bbepis/XUnity.AutoTranslator) or an existing Vietnamese translation.

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
2. Download the ZIP from the [latest release](https://github.com/zeldrisho/UnityVietnameseFontSync/releases/latest) and extract it into `<Game>/BepInEx/`. The plugin DLL should be at:
   ```text
   <Game>/BepInEx/plugins/zeldrisho.UnityVietnameseFontSync/UnityVietnameseFontSync.dll
   ```
3. Run the game once with the plugin installed to generate:
   ```text
   <Game>/BepInEx/config/zeldrisho.UnityVietnameseFontSync.cfg
   ```
4. Close the game and follow the setup steps below to discover font names and configure a matching rule.

The initial configuration makes **no font changes**. No game assemblies or proprietary font files are bundled.

## Configure and verify

### 1. Discover the font asset name

With the game closed, set `Diagnostics = true` under `[General]` in the generated config. Leave `Rules.Order` empty while discovering names. Start the game, visit the menus or screens you want to change, then inspect `Discovered TMP font:` entries in `BepInEx/LogOutput.log`. Match the TMP font asset name, not the text component name.

### 2. Add an exact Fallback rule

Close the game before editing. Create this directory and put a licensed Vietnamese-capable font file in it; fonts are not included in the release:

```text
<Game>/BepInEx/plugins/zeldrisho.UnityVietnameseFontSync/Fonts/
```

Use the following example, replacing `MyVietnameseFont.ttf` and the fictional asset name `MainMenu SDF` with your filename and discovered name. Other settings can retain their generated defaults.

```ini
[General]
Enabled = true
Diagnostics = true

[Fonts]
Profiles = menu

[Font.menu]
File = MyVietnameseFont.ttf

[Rules]
Order = menu-text
Exclude = Icons, Symbols

[Rule.menu-text]
Profile = menu
Match = Exact
Pattern = MainMenu SDF
Mode = Fallback
```

Fallback retains original glyphs and adds missing-character coverage. To replace incorrect existing glyphs or make accented and unaccented letters use the same replacement style, change only `Mode = Fallback` to `Mode = Synchronize`.

**Synchronize clears the matched font's whole character table in memory.** Use narrow rules and check symbols, untranslated text, and layout. Both modes preserve existing fallback chains and leave unmatched fonts unchanged. Matching outlines, shadows, spacing, and line breaks is not guaranteed. Restart without the plugin to restore the game's original assets.

### 3. Verify and turn off diagnostics

Restart the game. Check the log for a loaded profile and an `Applied` rule, then visually check the relevant screens. Successful log messages do not guarantee correct rendering or layout.

Once verified, close the game, set `Diagnostics = false`, and restart. This stops discovered-font-name logging. With at least one ready profile, scanning and font changes continue; otherwise, game-font enumeration is skipped while diagnostics are off. Normal status messages, warnings, and errors remain enabled; there is no separate plugin debug setting, and you do not need to disable BepInEx logging globally. Re-enable diagnostics when troubleshooting or discovering more fonts.

See [configuration](docs/configuration.md) for secondary font files, multiple profiles, matching modes, exclusions, and atlas settings.

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

See the [development guide](docs/development.md) for setup, build/test commands, runtime design, and validation limits, and the [release guide](docs/release.md) for packaging and publication.

## License

[MIT](LICENSE).
