# Security policy

## Supported versions

Security fixes are made for the latest release only.

| Version | Supported |
|---|---|
| 1.0.x (latest) | ✓ |
| Older | — |

## Scope

AVAFlight is an offline, single-player desktop game. It makes no network connections and has no accounts or servers. The areas where a security problem could matter are:

- **Save and settings files.** AVAFlight parses JSON files from its data directory, and players may share save files. A crafted save that causes code execution, writes outside the data directory, or crashes the game in a way that isn't a clear error message is in scope.
- **Single-file executables.** These are the published binaries and the native libraries they extract at startup (SkiaSharp, HarfBuzz, SDL3, and AvaloniaNative on macOS).
- **Build and release pipeline.** This covers the GitHub Actions workflows and the release artifacts.

Bugs in third-party libraries should also be reported upstream. AVAFlight will update its pinned versions when fixes are released.

## Reporting a vulnerability

**Please do not report security problems in public issues.**

- **Preferred:** use GitHub's private reporting at **[Report a vulnerability](https://github.com/Harlock123/AVAFlight-Starflight/security/advisories/new)** (also available from the repository's **Security** tab).
- **Alternatively:** email [harlock123@gmail.com](mailto:harlock123@gmail.com).

Please include:

- the AVAFlight version
- your platform
- steps to reproduce
- a sample file, if the problem involves a save or settings file

AVAFlight is maintained by a volunteer. You can expect an acknowledgement within a week. Fixes are released as soon as practical, and reporters are credited unless they prefer otherwise.
