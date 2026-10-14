# Configuration

File: `BepInEx/config/zeldrisho.UnityVietnameseFontSync.cfg`. Edit while the game is closed and restart after changes. Hot reload is not supported.

## Global settings

| Section | Key | Default | Behavior |
|---|---|---|---|
| General | Enabled | true | Enable plugin |
| General | Diagnostics | false | Log each live TMP font asset name once |
| General | ScanInterval | 2 | Discovery interval in seconds, 0–300; zero disables periodic scans, not startup/scene scans |
| General | RetryInterval | 30 | Delay before retrying a failed font mutation, 1–600 seconds |
| Fonts | Profiles | default | Comma-separated profile IDs |
| Fonts | CustomCharacters | empty | Characters added to the ASCII/Vietnamese preset for every profile |
| Rules | Order | empty | Comma-separated rule IDs in first-match order |
| Rules | Exclude | empty | Case-insensitive asset-name fragments excluded before all rules |

IDs contain letters, digits, underscores, or hyphens. Lists trim whitespace and remove exact duplicates. IDs/profile references are case-sensitive; asset matching is case-insensitive. Unlisted sections are inactive.

The preset covers printable ASCII and uppercase/lowercase precomposed Vietnamese. Add curly quotes, ellipsis, combining marks, or other required characters explicitly. Game text is not normalized or rewritten. Invalid surrogate sequences are rejected.

## Profiles: `[Font.<ID>]`

| Key | Default | Allowed values |
|---|---|---|
| File | empty | Font filename, relative path, or absolute path; empty disables profile |
| SamplingSize | 36 | 8–128 |
| Padding | 4 | 1–32 |
| AtlasSize | 2048 | 256, 512, 1024, 2048, 4096 |
| MultiAtlas | true | Allow additional dynamic atlas pages |

Relative paths search `<plugin DLL directory>/Fonts/` first, then the OS Fonts directory. Absolute paths are used directly. Paths must be valid on the machine running the game; a Linux `/mnt/c/...` path is not a Windows runtime path.

Profiles must successfully add the entire preset plus custom characters. Missing glyphs or atlas population failures disable the profile and log missing Unicode values where TMP supplies them. Mapped game fonts are left alone. Files are loaded once at startup; restart to retry after fixing a file.

## Rules: `[Rule.<ID>]`

| Key | Default | Behavior |
|---|---|---|
| Profile | default | An ID in `Fonts.Profiles` |
| Match | Exact | `Exact`, `Contains`, or `All` |
| Pattern | empty | Required for Exact/Contains; ignored for All |
| Mode | Fallback | `Fallback` or `Synchronize` |

- First match wins. An unavailable target profile does not fall through to another rule.
- `All` explicitly matches every non-excluded game font. `*` is not a wildcard in Exact/Contains.
- Fallback appends the profile after existing fallbacks; original glyphs remain unchanged.
- Synchronize inserts the profile first, freezes the original atlas, clears its whole character table, and rebuilds lookup data. Existing fallbacks remain afterward.
- Synchronize can break symbols and non-Latin text absent from the replacement and preserved fallbacks. There is no automatic icon detection or serif/sans classification.

## Multiple-profile example

```ini
[Fonts]
Profiles = body, menu
CustomCharacters = …“”

[Font.body]
File = BodyFont.ttf

[Font.menu]
File = MenuFont.ttf
SamplingSize = 40

[Rules]
Order = body, menu
Exclude = Icons, Symbols

[Rule.body]
Profile = body
Match = Contains
Pattern = BodyText
Mode = Fallback

[Rule.menu]
Profile = menu
Match = Exact
Pattern = MenuText SDF
Mode = Synchronize
```

Names are illustrative, not built-in game mappings. Start with exact Fallback rules; inspect coverage and appearance before enabling Synchronize.

## Replacing a manual UnityExplorer script

Manual C# font changes affect the current process; they are not plugin configuration. Disable any saved/startup font script, close the game completely, and start with this plugin to generate its configuration. Recreate the intended font-to-asset mappings as profiles and rules; there are no existing plugin settings to migrate.

For example, the previously tested two-style setup maps `MortyOld` asset-name fragments to `palab.ttf` (Palatino Linotype Bold) and `Feruka` fragments to `bahnschrift.ttf` (Bahnschrift), using Synchronize for both. Those are optional user mappings, not built-in defaults or recommendations for every game. Use separate rules for each group instead of an `else` rule replacing all remaining fonts. Verify the files exist and pass glyph preflight on the target machine.

Use Fallback when only missing-character coverage is needed. Use Synchronize when a matched font group should render both accented and ordinary letters through the selected profile, including games whose existing fallback already provides Vietnamese but looks visually inconsistent.

## Recovery

Invalid IDs, atlas sizes, unknown profile references, and empty matching patterns disable initialization. BepInEx may use defaults for malformed typed values; check logs and generated comments. No disk game assets are modified. Restarting without the plugin or manual font scripts returns to the game's original loaded assets.
