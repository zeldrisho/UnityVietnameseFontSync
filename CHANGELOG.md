# Changelog

All notable user-facing changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/2.0.0/),
and this project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Vietnamese glyph fallback for supported Unity Mono games using TextMeshPro.
- Optional font synchronization to keep accented and unaccented letters visually consistent.
- Multiple font profiles, ordered matching rules, and exclusions for fonts such as icons and symbols.
- Ordered fallback font files within a profile, with required glyph coverage checked across the complete chain.
- Configurable font files, atlas settings, and support for late-loaded game fonts.
- Warnings for missing font files, unsupported TMP APIs, and characters the selected font cannot provide.
