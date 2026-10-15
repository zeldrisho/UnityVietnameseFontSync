# Release

See the [development guide](development.md) for toolchain setup, build/test commands, runtime design, and the manual validation checklist.

## CI

Workflows use minimal permissions, timeouts, concurrency controls, and action SHA pins with stable version comments. Review dependency updates manually and retain lock files. Review the Unity compatibility baseline rather than blindly accepting engine upgrades.

CI restores in locked mode, checks formatting, builds, runs tests, checks packaging logic, and uploads only the plugin DLL. Both CI and release packaging install the pinned, hash-verified dependency from `scripts/requirements.txt`. The packager uses `defusedxml` to reject DTDs, entities, and external references when reading project metadata; this build-time dependency is not included in the plugin archive. It does not need Steam or private reference assemblies.

## Publish a release

1. Finish manual validation and review the MIT license and any third-party redistribution rights.
2. Set the project `Version`; move relevant user-facing `Unreleased` changes to `## [X.Y.Z] - YYYY-MM-DD` in `CHANGELOG.md` and retain `Unreleased` above it. Keep developer instructions, CI changes, and test implementation details out of the changelog.
3. Validate packaging locally, commit, and push a matching stable `vX.Y.Z` tag only when ready to publish.
4. The release workflow reruns CI, validates tag/version/changelog consistency, creates the allowlisted ZIP, and publishes a normal GitHub release automatically. **Tagging is the publication trigger; there is no draft/manual-publish step.**

Run local packaging from the repository root, with the Python environment activated, after building the DLL and adding the dated release entry:

```bash
python3 scripts/package.py --tag vX.Y.Z --dll src/bin/Release/netstandard2.1/UnityVietnameseFontSync.dll
```

## Archive layout and documentation links

Archives use the `UnityVietnameseFontSync-X.Y.Z.zip` pattern and contain exactly:

```text
plugins/zeldrisho.UnityVietnameseFontSync/UnityVietnameseFontSync.dll
plugins/zeldrisho.UnityVietnameseFontSync/README.md
plugins/zeldrisho.UnityVietnameseFontSync/CHANGELOG.md
plugins/zeldrisho.UnityVietnameseFontSync/LICENSE
```

Extract into `<Game>/BepInEx/`, not the game root. The install directory is derived from the project GUID; the DLL name remains the assembly name. Guides stay in the repository. During GitHub packaging, README guide links point to that repository's release tag; local packages without GitHub metadata use plain-text documentation references instead of broken local links.

To verify hosted guide links in a local package, supply the repository metadata:

```bash
GITHUB_REPOSITORY=zeldrisho/UnityVietnameseFontSync \
  python3 scripts/package.py --tag vX.Y.Z --dll src/bin/Release/netstandard2.1/UnityVietnameseFontSync.dll
```

This only creates local packaging output; it does not publish a release.

The ZIP is the only attached release asset. The generated `notes.md` becomes the GitHub release body, not an archive entry. Separate checksum files are optional and omitted for this small distribution. No fonts, game/BepInEx/Unity DLLs, generated config, debug symbols, or internal planning notes are included. Users supply fonts and generate per-game configuration on first launch.
