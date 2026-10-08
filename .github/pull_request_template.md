## What does this change?

<!-- A short description of the change and why it's needed. Link any related issue: "Fixes #123". -->

## Checklist

See [CONTRIBUTING.md](../CONTRIBUTING.md) for details.

- [ ] `dotnet test` passes, and new or changed behaviour has tests
- [ ] No original Starflight code, data, text, art or audio is included; any new text is newly written
- [ ] New rules or numbers carry an evidence label (`documented`, `derived`, `fan-measured`, `reconstruction`)
- [ ] Historical-behaviour changes are recorded in `docs/FIDELITY.md`, and new sources in `docs/RESEARCH.md`
- [ ] Modern-only features go through `Policy` and do not change Classic's rules or balance
- [ ] Save-format changes bump `GameState.CurrentSchemaVersion` and add a migration
- [ ] Screenshots regenerated with `tools/screenshots.sh` if a screen's look changed
- [ ] Third-party code or assets (if any) are MIT-compatible and listed in `docs/THIRD_PARTY.md`
- [ ] `CHANGELOG.md` has an entry under **Unreleased**
