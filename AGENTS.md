# Agent Instructions

## Toolchain

- Use stable .NET 10 via `global.json`, Python 3 for packaging checks, and `fd` for file discovery.
- Restore with `dotnet restore UnityVietnameseFontSync.slnx --locked-mode`.
- Stop and report missing development tools or dependencies.

## Commands

| Task | Command |
|---|---|
| Configuration/adapter tests | `dotnet test tests/UnityVietnameseFontSync.Tests.csproj -c Release` |
| Release packaging tests | `python3 -m unittest discover -s scripts -p 'test_*.py'` |
| Check formatting | `dotnet format whitespace UnityVietnameseFontSync.slnx --no-restore --verify-no-changes` |
| Build | `dotnet build UnityVietnameseFontSync.slnx -c Release --no-restore --warnaserror` |

## Key Conventions

- Edit plugin identity/version in `src/UnityVietnameseFontSync.csproj`; do not edit generated `MyPluginInfo` constants.
- Keep game paths/mappings, game DLLs, and proprietary fonts out of committed runtime/build configuration.
- Keep runtime compatibility checks capability-based; do not add game-name/version heuristics.
- Keep destructive synchronization opt-in and unmatched fonts unchanged.
- Update lock files with dependency changes; review Unity baseline upgrades for compatibility.
- Record user-visible changes under `Unreleased` in `CHANGELOG.md`.
- Do not deploy, publish, or commit unless requested.

## External References

| Need | File |
|---|---|
| User setup | `README.md` |
| Configuration contract | `docs/configuration.md` |
| Runtime design, build, testing, release | `docs/development.md` |
| Change history | `CHANGELOG.md` |
| CI | `.github/workflows/ci.yml` |
| Published releases | `.github/workflows/release.yml` |
