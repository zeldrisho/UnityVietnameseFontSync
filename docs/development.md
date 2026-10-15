# Development

## Toolchain and layout

- Stable .NET 10 SDK, Git, and Python 3 for release packaging/tests.
- `global.json` accepts installed stable .NET 10 SDK feature bands; CI pins SDK 10.0.112 to match the locally validated toolchain.
- `src/`: the plugin project; `tests/`: Unity-independent tests; `scripts/`: release packaging.
- Public compile-time packages and lock files make clean builds independent of game installations. Never commit or distribute game assemblies or proprietary fonts.

[Microsoft's .NET documentation](https://learn.microsoft.com/en-us/dotnet/) covers the [CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/), [formatting](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format), and [GitHub Actions](https://learn.microsoft.com/en-us/dotnet/devops/github-actions-overview).

## Commands

Run from the repository root. Create and activate a Python virtual environment, then install the hash-verified packaging dependency:

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install --require-hashes -r scripts/requirements.txt
```

Activate the environment again in new shells before running Python tests or packaging. On Windows, use `.venv\Scripts\Activate.ps1` in PowerShell instead of `source`.

```bash
dotnet restore UnityVietnameseFontSync.slnx --locked-mode
dotnet format whitespace UnityVietnameseFontSync.slnx --no-restore --verify-no-changes
dotnet build UnityVietnameseFontSync.slnx -c Release --no-restore --warnaserror
dotnet test tests/UnityVietnameseFontSync.Tests.csproj -c Release --no-build --no-restore
python3 -m unittest discover -s scripts -p 'test_*.py'
```

To apply formatting, omit `--verify-no-changes`. Output: `src/bin/Release/netstandard2.1/UnityVietnameseFontSync.dll`. Deploy only that DLL, manually, to the game's plugin directory. Build/test commands never deploy.

The project originated from official [BepInEx.Templates](https://github.com/BepInEx/BepInEx.Templates). Stable 1.4.0 generation failed under SDK 10.0.112; documented template 2.0.0-be.4 worked. That scaffolding exception does not require a bleeding-edge runtime. Restore/build does not require templates to remain installed.

Dependencies use exact versions plus lock files. BepInEx packages use the latest stable versions available in the configured feed when checked. UnityEngine.Modules 2022.3.0 is an intentional compile-only compatibility baseline, not a required game version; blindly selecting the newest Unity package could introduce newer runtime API dependencies. No Unity/TMP/game DLL ships with the plugin. `Version`, display name, and GUID live in `src/UnityVietnameseFontSync.csproj`; template tooling generates constants for the plugin attribute. The initial stable version is `1.0.0`, matching the .NET SDK default and using three-part SemVer. The SDK derives the assembly/file versions (`1.0.0.0` initially); no separate version constants are maintained.

## Runtime design

| File | Responsibility |
|---|---|
| `src/FontRules.cs` | Matching, list parsing, paths, Unicode preset |
| `src/PluginSettings.cs` | One BepInEx config; profiles, rules, validation |
| `src/TmpRuntime.cs` | Discover and validate TMP capabilities without a TMP compile-time reference |
| `src/UnityVietnameseFontSyncPlugin.cs` | Generation, discovery, mutation, rollback, refresh |

At startup the adapter probes loaded TMP types, required properties, character APIs, and a compatible multi-argument Font-to-SDF creation overload. Unsupported capabilities prevent mutation; scene/periodic scans retry discovery so late-loaded TMP assemblies can be handled, with warnings logged only once. Optional trailing creation parameters use their declared defaults. Version strings are not used to choose behavior. Native runtime failures can still occur and need in-game testing.

Profiles are preflighted independently. Startup, scene changes, and periodic scans select the first matching rule after exclusions. Live object references track successful patches; destroyed entries are pruned. Per-font failures attempt snapshot rollback and use the configured retry delay. Generated profiles have no links back to game fonts, avoiding newly introduced fallback cycles.

Snapshots preserve original fallbacks/atlas mode and, for Synchronize, characters. Only text whose primary font directly changed is refreshed; text using that asset indirectly through fallbacks may need normal game refresh. Strings are never temporarily cleared. Original glyph tables and atlas textures are not cleared.

Generated assets/pages are pinned. Best-effort restoration runs at plugin destruction, but used generated materials remain until process exit because cached submeshes/other plugins may reference them. Hot unloading and live configuration reload are not supported. Restoration may conflict with another plugin modifying the same font; restart is the recovery path.

## Validation

### Manual baseline and limits

The working starting point was C# run inside UnityExplorer, not an installed version of this plugin or an existing plugin configuration:

- User-reported menu/settings tests in Unity Mono used separate serif and sans-serif replacement profiles, sampling size 36, padding 4, 2048-square dynamic atlases, and ASCII/Vietnamese preloading. These manual results do not establish plugin runtime support.
- The manual synchronization approach froze matched original assets to Static, cleared their character tables and fallback lists, and assigned replacement fallbacks. The plugin offers narrow explicit Synchronize rules but deliberately preserves existing fallback chains and leaves unmatched fonts unchanged.
- Missing glyphs and visually incorrect existing glyphs are different problems. Removing only lookup entries did not resolve the manual failure; a dynamic original font could add glyphs again. A fallback alone also mixed accented/unaccented styles.
- A tested replacement font produced missing-character boxes. That observation is not a universal coverage claim about every font release; preflight each actual file. Never assume an atlas size guarantees sufficient capacity.
- Other Unity Mono testing logged successful XUnity fallback creation, but no complete replacement mapping or new-plugin rendering result has been verified in that environment.
- TMP package versions cannot be established from those logs alone. Original metrics/material behavior, all font names, gameplay screens beyond the reported menus, long-term XUnity interactions, and performance remain unverified. Do not carry forward speculative explanations or claims of perfect shader preservation.

A texture PNG alone is not a portable TMP font asset: glyph mappings, metrics, and other font data are also required. Runtime-generated fonts are not exported by this plugin. Restarting clears manual in-memory changes; disable startup scripts before testing the plugin independently.

### Automated and in-game checks

Unit tests exercise configuration, matching, paths, Unicode scalars, adapter capability checks, optional parameters, and failed glyph-preflight reporting. Packaging tests check version/tag validation, changelog requirements, the exact four-file layout, documentation links, deterministic archive contents, and rejection of XML DTDs/entities before output is created. These are not native rendering tests.

Optional local API-surface check (no game process or native font calls):

```bash
GAME_MANAGED_PATH="/path/to/Game_Data/Managed" \
  dotnet test tests/UnityVietnameseFontSync.Tests.csproj -c Release --filter ExternalGameApi
```

The optional check loads the selected assembly set in an isolated context and is explicitly skipped without the environment variable. It passed against both available Unity 2022-era and Unity 6-era game reference sets. No game names or paths are encoded in tests or runtime decisions.

Before claiming runtime support, manually test:

- Exact Fallback and Synchronize rules, multiple profiles, missing files/glyphs, and atlas exhaustion.
- All Vietnamese cases/tones, combining marks, punctuation, icons, original-language fallbacks, rich text, and weights.
- Outlines/shadows, spacing, wrapping, clipping, and small/large sizes.
- Startup, scenes, async loads, inactive text activation, retries, and long-session atlas growth.
- XUnity coexistence with its competing TMP font settings empty.
- Discovery/refresh timings and memory in multiple games. No rendering/performance claims are verified yet.

## CI and release

Workflows use minimal permissions, timeouts, concurrency controls, and action SHA pins with stable version comments. Review dependency updates manually and retain lock files. Review the Unity compatibility baseline rather than blindly accepting engine upgrades.

CI restores in locked mode, checks formatting, builds, runs tests, checks packaging logic, and uploads only the plugin DLL. Both CI and release packaging install the pinned, hash-verified dependency from `scripts/requirements.txt`. The packager uses `defusedxml` to reject DTDs, entities, and external references when reading project metadata; this build-time dependency is not included in the plugin archive. It does not need Steam or private reference assemblies.

To release:

1. Finish manual validation and review the MIT license and any third-party redistribution rights.
2. Set the project `Version`; move relevant user-facing `Unreleased` changes to `## [X.Y.Z] - YYYY-MM-DD` in `CHANGELOG.md` and retain `Unreleased` above it. Keep developer instructions, CI changes, and test implementation details out of the changelog.
3. Validate packaging locally, commit, and push a matching stable `vX.Y.Z` tag only when ready to publish.
4. The release workflow reruns CI, validates tag/version/changelog consistency, creates the allowlisted ZIP, and publishes a normal GitHub release automatically. **Tagging is the publication trigger; there is no draft/manual-publish step.**

Local packaging after adding the dated release entry:

```bash
python3 scripts/package.py --tag vX.Y.Z --dll src/bin/Release/netstandard2.1/UnityVietnameseFontSync.dll
```

The initial archive is `UnityVietnameseFontSync-1.0.0.zip`; future archives retain the `UnityVietnameseFontSync-X.Y.Z.zip` pattern. It contains exactly:

```text
plugins/zeldrisho.UnityVietnameseFontSync/UnityVietnameseFontSync.dll
plugins/zeldrisho.UnityVietnameseFontSync/README.md
plugins/zeldrisho.UnityVietnameseFontSync/CHANGELOG.md
plugins/zeldrisho.UnityVietnameseFontSync/LICENSE
```

Extract into `<Game>/BepInEx/`, not the game root. The install directory is derived from the project GUID; the DLL name remains the assembly name. Guides stay in the repository. During GitHub packaging, README guide links point to that repository's release tag; local packages without GitHub metadata use plain-text documentation references instead of broken local links.

The ZIP is the only attached release asset. The generated `notes.md` becomes the GitHub release body, not an archive entry. Separate checksum files are optional and omitted for this small distribution. No fonts, game/BepInEx/Unity DLLs, generated config, debug symbols, or internal planning notes are included. Users supply fonts and generate per-game configuration on first launch. Comparison links can be added once the repository remote and first release exist.
