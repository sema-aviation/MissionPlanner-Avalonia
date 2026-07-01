# Auto-updater — task list

MP-style file-diff auto-updater for the Avalonia port. No Velopack, no OS code-signing certs.
Self-hosted on GitHub Pages, in-app diff download, prompt → download → restart.

## Locked decisions

- **Transport/host:** GitHub **Pages** (artifact-based deploy — `actions/upload-pages-artifact` +
  `deploy-pages`, no branch, no git bloat) hosts the latest loose file tree + `manifest.json` +
  `manifest.sig`. GitHub **Releases** stays as-is for first-install zips + changelog. Both, complementary.
- **Manifest:** single `manifest.json` `{version, notes, files:[{path, sha256, size}]}`.
- **Hashing:** SHA-256 (`System.Security.Cryptography.SHA256`). Drop MD5/BouncyCastle-for-hashing.
- **Integrity:** Ed25519-sign `manifest.json` (BouncyCastle — already a dependency). Public key embedded
  as a const in the app; private key = GitHub Actions secret; verify before trusting any hash.
- **OS code-signing certs (Apple/Windows):** deferred, not in scope now. App stays unsigned
  (Gatekeeper/SmartScreen first-run warnings remain; self-downloaded files aren't quarantined).
- **Packaging:** all 3 platforms = self-contained **folder**. Switch Windows off single-file.
- **Diff granularity:** whole changed files (no binary deltas).
- **No beta channel.** Remove "Check for BETA updates" from Help screen.
- **No on/off toggle.** Always check on startup (silent unless newer, non-skipped). "Skip this version"
  persists via `Settings.Instance["update_skip_version"]`.
- **Manual check:** "Check for Updates" on Help screen runs the same flow; if newer, prompt →
  Install / What's new / Skip / Later → download → restart.
- **UX:** non-blocking banner (Install / What's new / Later), real progress bar + cancel,
  release notes shown inline, "Skip this version". Prompt → download → restart now. No silent install.
- **Base URL:** `https://sema-aviation.github.io/MissionPlanner-Avalonia/` (host-agnostic const).

## Tasks

### 1. Packaging / CI (`release.yml`) — DONE
- [x] Windows: dropped `PublishSingleFile`/`EnableCompressionInSingleFile` → self-contained folder.
- [x] CI step: `build/gen-manifest.py` emits `manifest.json` (version, notes URL, per-file relpath+sha256+size) per RID.
- [x] CI step: openssl Ed25519-sign `manifest.json` → base64 `manifest.sig` from secret `UPDATE_SIGNING_KEY`.
- [x] New `pages` job: assemble `win-x64/ osx-arm64/ linux-x64/` trees, `upload-pages-artifact` + `deploy-pages`.
- [x] Release zip upload kept (renamed artifacts to `dist-*`; Pages artifacts `pages-*`; release filters `dist-*`).

### 2. In-app updater (`Services/Updater.cs` — reworked) — DONE
- [x] `UpdateEngine.Rid()` from `OperatingSystem.Is*` + `RuntimeInformation.OSArchitecture`; local ver = `AppVersion.Number`.
- [x] `FetchManifestAsync`: GET manifest.json + .sig, Ed25519 verify (BouncyCastle), else `SecurityException`.
- [x] `Diff`: SHA-256 each local file, collect mismatches. (No stale-file deletion in v1 — deliberate.)
- [x] `DownloadAsync`: staging dir, per-file sha256 verify, `IProgress` + `CancellationToken`, `Parallel.ForEachAsync(3)`.
- [x] `Apply`: verify-all then swap live→`.old`/staged→live, rollback on failure.
      Win: throwaway `.cmd` (wait PID → xcopy → relaunch). mac/linux: in-place; mac re-signs (`codesign`).
- [x] Injectable `HttpClient` + install dir + base URL + pubkey (testable core).

### 3. UI / UX — DONE
- [x] Removed "Check for BETA Updates" button + `HelpViewModel` beta/API code.
- [x] "Check for Updates" → `Updater.CheckNowAsync()` (Install/What's new/Skip/Later prompt if newer).
- [x] Startup check wired in `App.axaml.cs` (fire-and-forget, silent on error/offline).
- [x] Progress + cancel via existing `ProgressReporter`; prompt via `Dialogs.Choice` (new helper).
- [x] "What's new" opens release notes URL; "Skip this version" persists.
- [~] Non-blocking banner: DEFERRED (reused modals — no toast infra; matches MP).

### 4. Tests — DONE
- [x] `UpdaterTests.cs`: download+swap, tampered-manifest rejected, hash-mismatch throws (install untouched),
      Apply rollback, IsNewer theory. openssl→BouncyCastle interop verified separately. 9 tests pass.

### 5. Manual setup (USER ACTION REQUIRED before first release)
- [ ] Add GitHub secret `UPDATE_SIGNING_KEY` = the Ed25519 private PEM (provided separately).
- [ ] Enable GitHub Pages: repo Settings → Pages → Source = "GitHub Actions".
- [ ] Docs: per-user install note (writable dir) — optional fast-follow.

## Parked (YAGNI / fast-follow)
- Brotli-compressed blobs on the wire (~40% smaller dlls, stdlib `BrotliStream`) — easy fast-follow.
- Background download + apply-on-quit (VSCode-style) — v1.1.
- Binary/bsdiff deltas, CDN/object store, telemetry, staged rollout, resumable downloads.
- OS code-signing certs (Apple notarization / Windows Azure Trusted Signing) — when polish/budget allows.
