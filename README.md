# v3dfy

v3dfy is a Windows desktop application for local, offline AI-assisted 2D to 3D
video conversion. It is built with .NET 10, WPF, C#, and MVVM, with the long-term
goal of shipping a self-contained Windows installer that includes the app,
runtime, FFmpeg tools, local AI engine, Python runtime if needed, model files,
configuration, and licenses.

## What v3dfy does

v3dfy guides a user through selecting a 2D video, analyzing it with bundled
FFprobe, preparing a conversion plan, previewing the local iw3 command, and
running a local conversion that generates a 3D video output. The app is designed
for offline execution and does not use cloud conversion services.

The current primary output containers are MP4 and MKV. MP4 is the main
compatibility target. MKV is intended for master/archive-style output and future
work around richer stream preservation. Direct AVI output is not a primary iw3
output path.

## Current status

v3dfy is in active development. The current development branch includes a working
local iw3 conversion flow when the expected local engine bundle is available on
the machine, plus live conversion logs, cancellation, CPU/RAM/GPU/VRAM metrics,
partial output handling, output profiles, and optional LG-compatible MP4
post-processing.

Manual validation has confirmed a real full movie conversion and playback of an
LG-compatible MP4 copy on one LG 47LM6200-UE 3D TV. This is valuable validation,
but it is not a guarantee that every source, layout, codec, storage device, or
TV firmware combination will work.

The repository should not be treated as a finished production installer yet.
Packaging, model coverage, compatibility presets, and release hardening are
still in progress.

## Key features

- Guided Rufus/Balena Etcher-style WPF workflow.
- Spanish and English language selector.
- Light and dark theme selector.
- Video selection by file picker and drag/drop.
- Local FFprobe-based video analysis.
- Local AI engine health checks.
- Dry-run mode when the engine bundle is missing.
- Conversion plan with output profile, container, quality, 3D intensity, layout,
  local model, and open-after-finish options.
- Generated iw3 command preview.
- Live conversion mode with stdout/stderr log, summary, cancel button, and
  process metrics.
- Safer partial output handling so failed or canceled conversions do not leave a
  normal-looking final file.
- Optional LG 3D TV 2012 compatible MP4 copy after a successful primary output.

## Supported platform

v3dfy targets Windows x64. It is a desktop WPF application and is not intended
for macOS, Linux, web, MAUI, WinUI, or Electron.

Development currently expects a prepared Windows machine with the required .NET
SDK and the local engine/runtime bundle arranged as documented. The final
distribution goal is a self-contained offline installer so end users do not have
to install .NET, Python, FFmpeg, CUDA toolkit, models, or other development
dependencies manually.

## Local/offline architecture

The application is split into app, core, infrastructure, and engine-specific
layers:

- `src/V3dfy.App` - WPF UI, view models, commands, localization-facing text.
- `src/V3dfy.Core` - domain models, planning, execution abstractions,
  recommendations, process metrics formatting.
- `src/V3dfy.Infrastructure` - local filesystem, process runner, bundled tool
  paths, FFprobe parsing, Windows GPU metrics collection.
- `src/V3dfy.Engine.Iw3` - iw3 command contract, command building, conversion
  execution, and LG compatibility post-processing request modeling.
- `tests/V3dfy.Tests` - unit tests for planning, command construction,
  execution behavior, localization source checks, and infrastructure logic.

The app should resolve tools from runtime-relative internal folders rather than
depending on globally installed Python, FFmpeg, or other tools.

## Engine/runtime layout overview

The intended packaged layout keeps local tools and engine assets inside the app
distribution:

- `tools/ffmpeg/win-x64` - bundled FFmpeg and FFprobe executables.
- `engine/iw3` - local iw3 engine bundle.
- `engine/iw3/python` - embedded Python runtime when required by the engine.
- `engine/iw3/nunif/iw3/pretrained_models` - local model files.
- `licenses` - required third-party license files.
- `packaging/inno` - installer packaging assets and scripts.

Large model files and generated media outputs should not be committed to git
unless they are intentionally handled through an approved artifact strategy.

## 2D to 3D conversion flow

1. Select a source video.
2. Analyze the source using bundled FFprobe.
3. Choose or review an output profile.
4. Review visible, editable settings such as container, quality, intensity,
   3D layout, local model, LG copy options, and open-after-finish behavior.
5. Review the conversion plan and iw3 command preview.
6. Start conversion.
7. v3dfy runs iw3 against a temporary partial output path.
8. If iw3 succeeds and the partial output exists, v3dfy promotes the partial file
   to the final primary output path.
9. If enabled, v3dfy creates an optional LG-compatible MP4 copy from the
   completed primary output using bundled FFmpeg.
10. If requested, v3dfy opens the successful final output with the OS default
    player.

Canceled or failed conversions clean up partial outputs and do not overwrite a
previous valid final output with a failed result.

## LG 3D TV 2012 compatibility notes

The LG 3D Full HD 2012 profile is an editable starting point for older LG passive
3D TVs. It currently favors MP4, H.264 video, Full HD output, and Side-by-Side
playback guidance based on manual testing with an LG 47LM6200-UE TV.

When the LG-compatible copy option is enabled, v3dfy first creates the normal
primary output. After that succeeds, it post-processes the primary output into a
separate MP4 copy intended for that TV class. The current compatibility copy path
is conservative and layout-aware, and the validated path is Half Side-by-Side
MP4.

This is not a promise of perfect TV compatibility. Compatibility can vary by
source file, audio stream, subtitle stream, USB device, TV firmware, selected
layout, and media server/player behavior.

## Output files explained

v3dfy may create more than one file depending on the selected profile and
options.

### Primary output

The primary output is the normal converted 3D video. It uses the selected
container, selected 3D layout, and generated filename suffix. Example:

```text
Movie.v3dfy.3d.hsbs.mp4
```

### Optional LG-compatible copy

When enabled for the LG 3D Full HD 2012 profile, v3dfy creates a second file
after the primary output succeeds. This is an FFmpeg post-processed MP4 copy for
LG playback testing, not a second direct iw3 output. The current audio strategy
copies audio from the primary output to preserve dialogue and channel layout
instead of relying on an implicit downmix. Example:

```text
Movie.v3dfy.3d.hsbs.lg3d.hsbs.mp4
```

If the optional LG copy fails, the primary output remains successful and is not
deleted. The app reports a warning for the copy.

### Partial/temp outputs during conversion

While converting, v3dfy writes to an internal partial output path instead of the
final output path. The partial file is promoted to the final path only after a
successful process exit and finalization check.

Canceled or failed conversions delete partial outputs when safe. Source videos
are not deleted.

## Known limitations

- Conversion time can be long, especially for full movies and high-quality
  settings.
- AI depth estimation can produce artifacts.
- Output quality depends on the selected model, source material, scene type,
  motion, lighting, and encoding settings.
- Model coverage and model selection are still being evaluated.
- LG 3D TV compatibility has been tested on real hardware but is not guaranteed
  for all files, settings, TV firmware versions, or playback paths.
- GPU metrics are best-effort. When process-level GPU engine counters are not
  available, the app may show global/adapter-level GPU and VRAM values instead.
- Optional compatibility post-processing can fail without invalidating the
  successful primary output.
- The final self-contained installer is not complete yet.

## Development setup overview

Development should happen on Windows x64 inside this repository. Install the
required .NET SDK for the current project version and prepare the local engine
bundle according to the project documentation. The app must not depend on
globally installed Python or FFmpeg for the final packaged product.

Useful documents:

- `docs/development-workflow.md`
- `docs/architecture.md`
- `docs/engine.md`
- `docs/packaging.md`
- `docs/iw3-bundle-intake.md`

## Build/test commands

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

For local environment checks:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\check-dev-env.ps1
```

Do not use integration, publishing, or packaging scripts unless the current task
explicitly calls for them.

## Packaging overview

The packaging goal is a Windows x64 installer that contains everything an end
user needs to run local/offline conversion:

- v3dfy WPF app.
- .NET self-contained runtime.
- FFmpeg and FFprobe.
- local AI engine.
- embedded Python runtime if required.
- iw3/nunif-compatible runtime files.
- pretrained models that are legally and technically approved for distribution.
- configuration files.
- third-party licenses.

Packaging work lives under `packaging/inno` and related scripts. The release
package should use runtime-relative paths and should not require users to install
development dependencies manually.

## Repository rules

- Do not commit generated videos or conversion outputs.
- Do not commit large model files unless an approved artifact strategy is in
  place.
- Do not commit the developer machine's local intake bundle.
- Keep local bundle paths runtime-relative.
- Keep source videos, test media, and manually generated artifacts outside git.
- Preserve the Windows-only WPF/.NET direction.
- Keep conversion tests fake-process based unless a task explicitly calls for
  integration validation.

## Roadmap / next work

- Compare more depth models and document quality/speed tradeoffs.
- Add a model selection and testing workflow.
- Improve output profiles and profile customization behavior.
- Optimize conversion speed and progress reporting.
- Improve compatibility outputs for more devices and layouts.
- Harden packaging and installer generation.
- Expand manual validation on real hardware and media players.

## Development note / AI assistance disclosure

v3dfy was developed with assistance from OpenAI Codex for implementation support,
refactoring, and test generation. Product direction, architecture decisions,
manual validation, hardware testing, 3D TV compatibility testing, and final
acceptance were supervised and performed by the project owner.

v3dfy fue desarrollado con apoyo de OpenAI Codex para implementación,
refactorización y generación de pruebas. La dirección del producto, decisiones de
arquitectura, validación manual, pruebas en hardware real, pruebas de
compatibilidad con TV 3D y aceptación final fueron supervisadas y realizadas por
el propietario del proyecto.
