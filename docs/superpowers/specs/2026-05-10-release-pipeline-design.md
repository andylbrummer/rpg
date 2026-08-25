# Build & Release Pipeline — Design Spec
Date: 2026-05-10
Status: design — Phase 2 deliverable; Phase 1 builds locally only
Depends on: content-pipeline spec, asset-pipeline spec
Scope: CI builds, versioning, signing, installers, auto-update, distribution. Photino-based desktop app.

## 1. Targets

| Platform | Architecture | Installer | Notes |
|---|---|---|---|
| Windows | x64, arm64 | MSI + portable ZIP | code-signed |
| macOS | universal (x64 + arm64) | DMG + .app | notarized + signed |
| Linux | x64 | AppImage + tar.gz | Phase 2 add Flatpak |

Phase 1: Linux only (current dev). Phase 1.5: Windows added. Phase 2: macOS. Phase 3: arm64 Windows.

## 2. Versioning

SemVer: `MAJOR.MINOR.PATCH`. Pre-release: `0.x.y` until 1.0.

- MAJOR: save format breaks, content not back-compatible.
- MINOR: new features, save back-compatible via migration.
- PATCH: bug fixes, no save changes.

Build metadata appended: `0.4.0+build.1234.gabc123` (build number + git short sha).

Version stored at `src/engine/RPC.Engine/Version.cs`:

```csharp
public static class Version {
    public const string Semver = "0.4.0";
    public const string Build = "1234";
    public const string GitSha = "abc123";
    public static string Full => $"{Semver}+build.{Build}.g{GitSha}";
}
```

Generated from CI; committed as fallback for local dev.

## 3. CI workflow

`.github/workflows/release.yml`:

```yaml
on:
  push: { tags: ['v*'] }
  workflow_dispatch:

jobs:
  build:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '9.0.x' }
      - uses: actions/setup-node@v4
        with: { node-version: '20' }
      - run: npm ci --prefix src/client
      - run: npm run build --prefix src/client
      - run: dotnet run --project tools/asset-pipeline -- build
      - run: dotnet run --project tools/content-pack -- build content/ build/content.rpk
      - run: dotnet publish src/engine/RPC.Host -c Release -r ${{ matrix.rid }} --self-contained -p:PublishSingleFile=true
      - run: dotnet test
      - name: Package
        run: dotnet run --project tools/packager -- --os ${{ matrix.os }}
      - uses: actions/upload-artifact@v4
        with: { name: rpc-${{ matrix.os }}, path: dist/* }

  sign:
    needs: build
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/')
    steps:
      - ... fetch artifacts, sign per platform, re-upload ...

  release:
    needs: sign
    runs-on: ubuntu-latest
    if: startsWith(github.ref, 'refs/tags/')
    steps:
      - uses: softprops/action-gh-release@v2
        with:
          files: dist/*
          generate_release_notes: true
```

Matrix RIDs: `linux-x64`, `win-x64`, `osx-arm64` + `osx-x64` combined into universal.

## 4. Tools/packager

`tools/packager/` builds platform installers:

```
packager/
  WindowsMsi.cs       // WiX-based MSI builder
  MacDmg.cs           // create-dmg wrapper
  LinuxAppImage.cs    // appimagetool wrapper
  PortableZip.cs      // ZIP all platforms
```

Each step bundles:
- Published .NET host (`RPC.Host`)
- Built client (`src/client/dist/`)
- Content RPK (`build/content.rpk`)
- Asset RPK (`build/assets.rpk`) Phase 2
- VERSION file, LICENSE, README

## 5. Signing

### 5.1 Windows

EV code-signing cert (HSM-backed). `signtool sign /sha1 <thumbprint> /fd SHA256 /tr http://ts.url /td SHA256 <exe>`.

Cert stored in CI secret + KeyVault. Never committed.

### 5.2 macOS

- Sign each binary with Developer ID Application cert.
- Notarize via `xcrun notarytool submit --wait`.
- Staple notarization ticket to DMG.
- Gatekeeper accepts on user machine.

Cert + notary credentials in CI secret.

### 5.3 Linux

No required signing. Provide GPG-signed checksums for download verification:

```
sha256sum dist/*.AppImage > SHA256SUMS
gpg --detach-sign SHA256SUMS
```

GPG key fingerprint published on project site.

## 6. Auto-update (Phase 2)

In-app update check:

1. On startup (settings.gameplay.checkUpdates = true), HTTPS GET to `https://updates.{game-domain}/latest.json`.
2. Returns: `{ "version":"0.5.0", "downloadUrl":"...", "signature":"...", "releaseNotes":"..." }`.
3. If newer than current, surface non-blocking notification in TopBar.
4. User clicks → opens release notes modal → Download button.
5. Download to temp, verify signature, prompt restart.
6. On restart, launcher executes updater (small native binary) which swaps in the new build.

Updates delivered as differential patches (Phase 3) or full installer (Phase 2).

Settings: `autoCheck` (default on), `autoDownload` (default off), `channel` (`stable`|`beta`|`dev`).

## 7. Channels

| Channel | Cadence | Audience |
|---|---|---|
| `stable` | every 2-4 weeks | players |
| `beta` | weekly | testers |
| `dev` | per-commit on main | developers |

Each channel has its own `latest.json`. Users opt in per-channel; downgrading channel doesn't downgrade installed version (warns).

## 8. Release tagging

Convention: `v<semver>` (e.g., `v0.4.0`). CI workflow triggers on tag push.

Pre-release tags: `v0.5.0-beta.1`. Release notes auto-include commits since last tag in same channel.

Tag protected: only maintainers can push tags. Verified by branch protection rule.

## 9. Build determinism

Where possible:
- `dotnet publish` with `-p:DeterministicSourcePaths=true -p:ContinuousIntegrationBuild=true`.
- Vite build: `vite build` with stable hash bases (default).
- Asset pipeline: deterministic outputs per content-pipeline + asset-pipeline specs.

Goal: same source + content hash → byte-identical artifacts. Verified by separate CI job comparing two builds of the same commit.

Imperfect for binaries with embedded timestamps; document deviations.

## 10. Artifact storage

CI artifacts retained 90 days. Production releases retained indefinitely on GitHub Releases.

Mirror to S3-compatible storage Phase 3 for faster downloads + CDN.

## 11. License + attribution

Every release includes:
- `LICENSE.txt` (project license).
- `THIRD_PARTY_NOTICES.txt` generated from `dotnet list package` + `npm ls --omit=dev`.
- Font licenses (asset spec §10).
- Audio asset licenses.

CI fails if a new dependency lacks a compatible license entry.

## 12. Pre-release smoke

Before publishing tag:

1. CI runs full test suite (xUnit + Vitest + Playwright).
2. CI starts the built binary on Linux + Windows runners, launches Photino, hits health endpoint, takes screenshot. Compares to baseline.
3. Manual playthrough required for major tags (>= MINOR); checklist in `docs/release-checklist.md` (Phase 2).

## 13. Rollback

If a release breaks: tag pulled from `latest.json` within 1 hour. `latest.json` reverts to previous stable. Users with auto-check see no update available.

Catastrophic save corruption bug: emergency patch released same day. Tag advanced past broken version.

Communication: project blog / Discord (Phase 3 community spec).

## 14. Tests

- xUnit: packager produces expected file layout per platform.
- Manual: install MSI → launch → play 5 min → uninstall cleanly.
- Manual: macOS DMG → drag to Applications → launch → no Gatekeeper prompt → quit.
- Manual: Linux AppImage → `chmod +x` → run → state persists between runs.

## 15. Out of scope

- App store distribution (Steam / itch) — Phase 3 separate spec.
- Anti-piracy DRM.
- Beta tester invite system.
- Crash uploader infrastructure (covered by telemetry spec).
