# Packaging

## Publish target

v3dfy targets a self-contained Windows x64 publish:

```powershell
.\scripts\publish-win-x64.ps1
```

The script publishes `src/V3dfy.App/V3dfy.App.csproj` to
`artifacts/publish/v3dfy-win-x64`. A normal publish does not force iw3 bundle
validation or staging:

```powershell
.\scripts\publish-win-x64.ps1
```

The default path remains stable and the script preserves the current
repository support-folder copy behavior for `tools`, `engine`, and `licenses`
when those directories exist. Prefer the explicit staging options below for a
real iw3 bundle so the bundle is validated before it is copied.

The expected local engine bundle contract is documented in `docs/engine.md`.
Packaging must preserve that inspectable layout, including
`engine/iw3/ENGINE_MANIFEST.json`, `engine/iw3/python/python.exe`,
`engine/iw3/python/python312._pth`, an iw3 entry file at
`engine/iw3/nunif/iw3/__main__.py`, and
`engine/iw3/nunif/iw3/pretrained_models`, including required non-depth iw3
runtime dependencies such as
`engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/iw3_row_flow_v3_20250627.pth`.

The staged bundle root contains `python` and `nunif` as sibling folders. Keep
that relationship intact because the embedded `python312._pth` uses
`..\nunif`.

Before packaging a local iw3 bundle, validate its structure without running the
engine:

```powershell
.\scripts\validate-iw3-bundle.ps1
.\scripts\validate-iw3-bundle.ps1 -StrictModels -RequireCapabilities
```

After validation, stage the bundle into an app or publish layout:

```powershell
.\scripts\stage-iw3-bundle.ps1 -TargetRoot artifacts\publish\v3dfy-win-x64
```

The publish script can also validate and stage an iw3 bundle as part of the
publish flow:

```powershell
.\scripts\publish-win-x64.ps1 -Iw3BundleRoot C:\path\to\iw3-bundle
.\scripts\publish-win-x64.ps1 -Iw3BundleRoot C:\path\to\iw3-bundle -StrictModels -RequireCapabilities
.\scripts\publish-win-x64.ps1 -IncludeIw3Bundle
```

`-Iw3BundleRoot` stages the supplied bundle. `-IncludeIw3Bundle` uses the
repo-local `engine\iw3` bundle when no bundle root is supplied. When either
option is used, publish skips the broad post-publish `engine` copy and calls
`scripts/stage-iw3-bundle.ps1` with the publish directory as the target. The
staged runtime layout is always:

```text
artifacts/publish/v3dfy-win-x64/engine/iw3
```

Validation runs before staging. If validation fails, staging fails and the iw3
bundle is not copied by the staging script. The validation and staging scripts
inspect and copy local files only. They do not run Python, iw3, FFmpeg, model
loading, downloads, or installs.

## Why PublishSingleFile is disabled

`PublishSingleFile` is intentionally not enabled by default. The local engine,
embedded runtime, FFmpeg tools, models, configuration, and license files need a
clear inspectable directory layout. Keeping these components separate also
simplifies diagnostics and future updates.

## Release installer options

The release should offer three options, all based on the same split portable
payload:

1. Recommended web installer:
   `artifacts\installer\v3dfy-v0.1.0-preview.1-web-setup.exe`

   Users download one small setup EXE. Internet is required during installation
   because the setup helper downloads the shared
   `.part01`, `.part02`, and `.part03` payload files from the configured
   GitHub Release. The installer verifies SHA256 for each part, rebuilds the
   portable ZIP, verifies the final ZIP SHA256, installs into Program Files,
   creates a Start Menu shortcut, optionally creates a Desktop shortcut, and
   deletes temporary downloaded and rebuilt files after a successful install.
   During payload setup, a classic installer-style v3dfy setup progress window
   shows the current status, MB/GB and percent progress where available, and a
   large timestamped scrolling setup log for downloads, SHA256 verification,
   ZIP rebuild, extraction, install, and cleanup.
   The installed app works offline afterward.

2. Offline installer:
   `artifacts\installer\v3dfy-v0.1.0-preview.1-offline-setup.exe`

   Users keep the offline setup EXE and all payload `.part` files in the same folder,
   then double-click the setup EXE. No internet and no PowerShell are required during
   installation. The installer finds the same split payload
   parts beside the setup EXE, verifies SHA256 for each part, rebuilds the ZIP,
   verifies the final ZIP SHA256, installs into Program Files, creates a Start
   Menu shortcut, optionally creates a Desktop shortcut, and deletes the
   temporary rebuilt ZIP after a successful install.
   During payload setup, a classic installer-style v3dfy setup progress window
   shows local part discovery, SHA256 verification, ZIP rebuild, extraction,
   install, cleanup, and a large timestamped scrolling setup log.

3. Portable split ZIP:
   `v3dfy-v0.1.0-preview.1-win-x64-portable.zip.part01` through `.part03`

   This is a technical fallback for users who need a portable layout or manual
   extraction. Prefer the web installer for normal users and the offline
   installer for air-gapped or preloaded installs.

Build the release payload from the current publish output before building
installers:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release-payload.ps1 -Version 0.1.0-preview.1
```

That script zips `artifacts\publish\v3dfy-win-x64` into
`artifacts\release\v3dfy-v0.1.0-preview.1-win-x64-portable.zip`, removes stale
split payload parts, writes `.part01`, `.part02`, and `.part03` under
`artifacts\release\split`, and writes `SHA256SUMS.txt`.

Build both release installers from the fresh shared split payload with:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release-installers.ps1 -Version 0.1.0-preview.1 -ReleaseBaseUrl "https://github.com/Henrriegel/v3dfy/releases/download/v0.1.0-preview.1" -PayloadPartsDir ".\artifacts\release\split"
```

The installer packaging script fails if the release portable ZIP or any split
part is missing, if the ZIP is older than the publish output, or if the split
parts do not recombine to the ZIP. This prevents compiling fresh installers
around a stale app payload.

The packaging script defaults to:

- Release base URL:
  `https://github.com/Henrriegel/v3dfy/releases/download/v0.1.0-preview.1`
- Payload parts directory: `artifacts\release\split`
- Installer output directory: `artifacts\installer`

Generated assets:

- `artifacts\release\v3dfy-v0.1.0-preview.1-win-x64-portable.zip`
- `artifacts\release\split\v3dfy-v0.1.0-preview.1-win-x64-portable.zip.part01`
- `artifacts\release\split\v3dfy-v0.1.0-preview.1-win-x64-portable.zip.part02`
- `artifacts\release\split\v3dfy-v0.1.0-preview.1-win-x64-portable.zip.part03`
- `artifacts\installer\v3dfy-v0.1.0-preview.1-web-setup.exe`
- `artifacts\installer\v3dfy-v0.1.0-preview.1-offline-setup.exe`
- `artifacts\installer\README_WEB_INSTALLER.txt`
- `artifacts\installer\README_OFFLINE_INSTALLER.txt`
- `artifacts\installer\SHA256SUMS.installers.txt`

The web and offline installers are Inno Setup bootstrap installers. They bundle
only a small self-contained Windows x64 setup helper plus a payload manifest.
They do not duplicate the 5.4 GB payload into Inno `.bin` files. The helper
does not require the .NET SDK, Python, Git, FFmpeg, iw3, or external tools on
the user's machine.

Uninstall removes the complete `{app}` install tree. This includes the app,
bundled tools, bundled iw3 engine, embedded Python, bundled models, model packs
imported under `engine\iw3\nunif\iw3\pretrained_models`, and stale payload
files left under the install directory. User videos, converted outputs, and
AppData are outside `{app}` and are not part of installer uninstall cleanup.

## Legacy embedded installer

The older installer target is Inno Setup and embeds the published app folder.
It is useful only for small local test packages because a full release payload
exceeds GitHub Release single-asset limits. Run packaging preflight without
compiling the legacy embedded installer with:

```powershell
.\scripts\package-inno.ps1 -PreflightOnly
.\scripts\package-inno.ps1 -RequireIw3Bundle -PreflightOnly
.\scripts\package-inno.ps1 -RequireIw3Bundle -StrictModels -RequireCapabilities -PreflightOnly
```

Normal installer preflight checks the publish output:

- `artifacts\publish\v3dfy-win-x64` exists.
- `V3dfy.App.exe` exists.
- `tools\ffmpeg\win-x64\ffmpeg.exe` exists.
- `tools\ffmpeg\win-x64\ffprobe.exe` exists.

For a full offline installer that must include iw3, require the staged bundle
preflight. With `-RequireIw3Bundle`, packaging also checks
`artifacts\publish\v3dfy-win-x64\engine\iw3` and calls
`scripts\validate-iw3-bundle.ps1` against that published bundle. `-StrictModels`
and `-RequireCapabilities` are passed through to the bundle validator.

After preflight passes, build the installer with:

```powershell
.\scripts\package-inno.ps1
.\scripts\package-inno.ps1 -RequireIw3Bundle
.\scripts\package-inno.ps1 -RequireIw3Bundle -StrictModels -RequireCapabilities
```

If preflight fails, Inno packaging is skipped. With `-PreflightOnly`, Inno
packaging is skipped after successful preflight. Packaging preflight inspects
files only. It does not run the app, Python, iw3, FFmpeg, model loading,
downloads, or installs.

Before release, verify that the published app works on a clean Windows x64
machine without global .NET, Python, FFmpeg, models, or development tools.
Run release packaging in this order: publish `artifacts\publish\v3dfy-win-x64`,
run `scripts\package-release-payload.ps1`, then run
`scripts\package-release-installers.ps1`. Run installer packaging only after the
publish output contains the expected `engine\iw3` layout for the bundle you
intend to ship.

## Runtime and user data paths

Installed app runtime dependencies are resolved relative to the app base
directory, not a developer checkout or a fixed drive. Bundled FFmpeg tools are
expected at `tools\ffmpeg\win-x64\ffmpeg.exe` and
`tools\ffmpeg\win-x64\ffprobe.exe` under that runtime root. The bundled iw3
engine is expected under `engine\iw3`, including
`engine\iw3\python\python.exe`, `engine\iw3\nunif\iw3\__main__.py`,
`engine\iw3\nunif\iw3\pretrained_models`, and the row-flow runtime dependency
at
`engine\iw3\nunif\iw3\pretrained_models\hub\checkpoints\iw3_row_flow_v3_20250627.pth`.

End users should not install .NET, Python, FFmpeg, iw3, models, or development
tools separately. The installer must provide the app, self-contained .NET
runtime, FFmpeg/FFprobe, local engine runtime, models, configuration, and
licenses needed for offline use.

Writable app data lives under the user's local app data folder. Preview source
clips, preview partials, temporary preview files, accepted preview outputs, and
preview cleanup are contained under `%LOCALAPPDATA%\v3dfy\previews`. Logs live
under `%LOCALAPPDATA%\v3dfy\logs`.

Final conversion partial files are derived from the selected final output path
and tracked per active conversion attempt. On cancel or failure, v3dfy deletes
only that tracked partial file after the conversion process has stopped. It does
not scan the source or output folder for wildcard cleanup and does not delete
the source video, completed final output, or unrelated files.
