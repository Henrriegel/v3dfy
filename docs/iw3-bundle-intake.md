# nunif/iw3 Bundle Intake Plan

This plan describes how to turn a real prepared nunif/iw3 Windows installation
into the v3dfy `engine/iw3` bundle layout. It is a development intake flow, not
an end-user install flow.

Do not run this intake from the installed app. The final v3dfy installer must
already contain the prepared runtime, engine files, models, configuration, and
license notices needed for offline use.

## Scope Boundaries

Development preparation:

- May use internet access, the official nunif Windows package, its installer or
  update scripts, and `python -m iw3 -h`.
- Must happen in an external or disposable preparation location, for example
  `C:\v3dfy-iw3-intake\nunif-prepared`, a disposable VM, or another temporary
  developer-only folder.
- Must not write directly into the final `engine/iw3` bundle until the prepared
  layout has been inspected and mapped.

Final app/install:

- Must be offline and self-contained.
- Must not require the end user to run the nunif installer, install Python,
  install Git, install Python packages, install FFmpeg, download models, or use
  internet access on first run.
- Must include required licenses, notices, attribution, and model provenance in
  `licenses/`.

Runtime execution:

- Must use paths resolved from the app/runtime root.
- Must use `engine/iw3/python/python.exe` for iw3 execution.
- Must not rely on global `PATH`, global Python, a developer checkout, or a
  publish/debug/install directory hardcoded in app code.

CLI verification:

- Run `python -m iw3 -h` only during bundle preparation against the exact
  bundled iw3 version.
- Record the verified result in `engine/iw3/IW3_CLI_CAPABILITIES.json`.
- Do not run iw3 during normal app startup to discover capabilities.
- Keep unverified iw3 options out of `Iw3CommandBuilder`; the confirmed base
  command remains `python -m iw3 -i <input> -o <output>`.

## Intake Flow

1. Prepare a real nunif/iw3 installation in an external development folder.
   Use the official upstream Windows package or another controlled preparation
   method there, not inside `engine/iw3`.

2. Let the upstream preparation complete all online work in that external
   folder. This includes embedded Python acquisition, repository files, Python
   dependencies, and pretrained model downloads. iw3 can download large model
   files on first run in upstream usage, so the preparation pass must trigger
   or otherwise populate every model file that v3dfy intends to ship before
   packaging.

3. Inspect the prepared folder structure and record the exact source paths for
   Python, iw3 entry files, Python packages, model files, configuration files,
   and licenses. The exact upstream Windows package layout is not assumed by
   v3dfy and must be verified from the prepared installation.

4. Create a clean candidate bundle folder that already matches v3dfy's runtime
   contract. The candidate may be outside the repo during inspection. Its root
   must be the folder that will become `engine/iw3`.

5. Copy only the required runtime pieces into the candidate bundle. Do not copy
   installer caches, download caches, Git metadata, temporary build files,
   source archives that are not needed at runtime, or developer-only logs.

6. Create `ENGINE_MANIFEST.json` in the candidate bundle. It must not contain a
   placeholder version. Record the selected nunif/iw3 version, preparation date,
   runtime source, model set, and any GPU/CPU assumptions that were verified.

7. During bundle preparation only, run the candidate embedded Python with
   `-m iw3 -h` against the exact bundled iw3 version. Record the result in
   `IW3_CLI_CAPABILITIES.json`. The manifest should set
   `verifiedBaseCommand=true` only if the base command was verified for that
   version. Keep planning-only options in `unverifiedOptions` until each one is
   confirmed against the bundled CLI.

8. Create `nunif/iw3/pretrained_models/MODEL_CATALOG.json` if friendly model
   names or model-purpose metadata are useful. The catalog is optional
   diagnostics and selection metadata; model files still need to exist under
   `nunif/iw3/pretrained_models`.

9. Review redistribution obligations before staging into a release bundle.
   Include license texts and notices for nunif/iw3, embedded Python, Python
   packages, PyTorch or other ML frameworks, PyAV/FFmpeg-related components,
   FFmpeg builds, and every pretrained model.

10. Validate the candidate layout without running the engine:

    ```powershell
    .\scripts\validate-iw3-bundle.ps1 -BundleRoot C:\path\to\candidate-iw3 -StrictModels -RequireCapabilities
    ```

11. Stage the validated candidate into a test app or publish layout:

    ```powershell
    .\scripts\stage-iw3-bundle.ps1 -SourceBundleRoot C:\path\to\candidate-iw3 -TargetRoot artifacts\stage\v3dfy-app -CleanTarget -StrictModels -RequireCapabilities
    ```

    For publish output, use:

    ```powershell
    .\scripts\publish-win-x64.ps1 -Iw3BundleRoot C:\path\to\candidate-iw3 -StrictModels -RequireCapabilities
    ```

12. Package only after publish preflight accepts the staged bundle:

    ```powershell
    .\scripts\package-inno.ps1 -RequireIw3Bundle -StrictModels -RequireCapabilities -PreflightOnly
    .\scripts\package-inno.ps1 -RequireIw3Bundle -StrictModels -RequireCapabilities
    ```

## Target Bundle Mapping

The observed prepared nunif Windows package already maps naturally to the
candidate bundle root: `python` and `nunif` are sibling folders. Preserve that
relationship because `python/python312._pth` contains `..\nunif`.

Observed prepared package facts:

- nunif commit: `d23721f1b5f0a4c92c3ee1be013180bf298730c5`.
- Approximate prepared size: `python` 6.15 GB, `nunif` 1.72 GB, iw3
  `pretrained_models` 1.25 GB.
- CLI entry: `nunif/iw3/__main__.py`.
- Model root: `nunif/iw3/pretrained_models`.
- Large detected model file:
  `nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_metric_depth_indoor.pt`.
- No top-level `iw3.py` was found in the prepared tree.

Map the prepared nunif installation into this candidate bundle layout:

```text
candidate-iw3/
  ENGINE_MANIFEST.json
  IW3_CLI_CAPABILITIES.json # optional unless packaging requires it
  python/
    python.exe
    python312._pth
    <embedded Python runtime files>
    <Python site-packages and dependency folders required by nunif/iw3>
  nunif/
    iw3/
      __main__.py
      pretrained_models/
        MODEL_CATALOG.json # optional v3dfy metadata
        <approved pretrained model files>
      <nunif/iw3 package files required for python -m iw3>
    <other nunif repository files required at runtime>
  <other runtime config files required by the prepared package>
  <license or notice files that must live beside the engine, if any>
```

The staged runtime layout under v3dfy is:

```text
<app root>/engine/iw3
```

Required v3dfy paths after staging:

- `engine/iw3/python/python.exe`
- `engine/iw3/python/python312._pth`
- `engine/iw3/nunif`
- `engine/iw3/nunif/iw3`
- `engine/iw3/nunif/iw3/__main__.py`
- `engine/iw3/nunif/iw3/pretrained_models`
- `engine/iw3/ENGINE_MANIFEST.json`
- `engine/iw3/IW3_CLI_CAPABILITIES.json` for full capability-required
  packaging
- Python site-packages and dependency folders reachable by the embedded Python
  without global Python or `PATH`
- Required license and notice files, with distribution copies under `licenses/`

## Files Not To Carry Forward

Exclude these unless a later runtime verification proves they are required:

- Upstream installer files and downloaded archives.
- Git working metadata such as `.git`.
- Build caches, wheel caches, pip caches, and temp folders.
- Logs from the development preparation run.
- Placeholder manifests, placeholder model files, and documentation-only
  contract files.
- Any model file not selected for v3dfy's supported offline bundle.

## Remaining Verification

- Reconfirm the prepared folder structure, embedded Python path file, iw3 entry,
  and model set whenever the selected nunif package or commit changes.
- Decide which pretrained model files are required for v3dfy's intended
  presets.
- Whether PyAV or other video-related Python packages bring additional binary
  dependency and license requirements beyond the separate FFmpeg executables
  bundled by v3dfy.
- The exact supported iw3 CLI flags for the selected bundled version, verified
  with `python -m iw3 -h` during preparation.
- GPU and CPU compatibility expectations for the selected package, including
  CUDA or driver prerequisites that cannot be bundled.
- Redistribution terms for nunif/iw3, Python, PyTorch, each Python package,
  FFmpeg-related components, and every model.

Do not treat the bundle as release-ready until these unknowns are resolved and
the staged publish output passes `package-inno.ps1 -RequireIw3Bundle
-StrictModels -RequireCapabilities -PreflightOnly`.
