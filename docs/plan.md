# Remaining work

## Runtime validation

- [ ] Run the new runtime-adapter plugin in multiple Unity Mono games; record actual BepInEx, Unity, and TMP versions.
- [ ] Verify startup and late TMP assembly loading, missing/stripped capabilities, and native font creation failures.
- [ ] Validate Fallback/Synchronize independently, failed profile safety, symbols, combining marks, original-language fallbacks, rich text, and weights.
- [ ] Compare outlines, shadows, spacing, wrapping, and clipping at different sizes.
- [ ] Test scenes, late assets, inactive text activation, retries, and long-session multi-atlas memory behavior.
- [ ] Verify indirect fallback users refresh; extend refresh propagation if required.
- [ ] Verify XUnity coexistence and measure discovery/refresh costs.
- [ ] Add Unity integration coverage for mutation rollback and cleanup; API-surface and fake-adapter tests do not verify native rendering.

## Release readiness

- [ ] Confirm redistribution rights for any future third-party resources; plugin code and documentation now use MIT.
- [ ] Run the workflows on GitHub after a repository remote is configured; local checks cannot verify hosted artifact/release permissions.
- [ ] Validate the release archive and runtime behavior before pushing a version tag; the workflow publishes a normal release automatically.
- [ ] Add changelog tag/comparison links once the remote and first release exist.
- [ ] Recheck stable BepInEx.Templates generation with newer SDKs; the existing project does not need template regeneration.

The two permanent guides are [configuration.md](configuration.md) and [development.md](development.md). Architecture notes are consolidated into development rather than maintained as a third guide.
