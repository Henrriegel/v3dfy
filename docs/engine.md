# Local Engine Layout

## Bundle contract

The final offline bundle must use this local layout:

```text
engine/iw3/
  ENGINE_MANIFEST.json
  IW3_CLI_CAPABILITIES.json # optional verified CLI metadata
  python/
    python.exe
  iw3.py                 # one supported entry option
  iw3/
    __main__.py          # alternate supported entry option
  models/
    MODEL_CATALOG.json   # optional local metadata
    <approved model files>
tools/ffmpeg/win-x64/
  ffmpeg.exe
  ffprobe.exe
```

The iw3 engine entry can be either `engine/iw3/iw3.py` or
`engine/iw3/iw3/__main__.py`. The manifest must include a real engine version;
`"version": "placeholder"` is treated as missing. Supported model file
extensions are `.pth`, `.pt`, `.onnx`, `.safetensors`, `.ckpt`, and `.bin`.

## Confirmed iw3 CLI contract

The confirmed base invocation for nunif/iw3 is:

```text
python -m iw3 -i <input file or directory> -o <output file or directory>
```

v3dfy must use the bundled Python executable from `engine/iw3/python/python.exe`
and pass `-m`, `iw3`, `-i`, and `-o` as structured process arguments. It must
not rely on `PATH` or shell execution.

Other conversion controls are v3dfy planning metadata until they are verified
against the exact bundled iw3 version. This includes selected model, 3D layout,
codec, quality, intensity/depth, scene detection, normalization, and
convergence/divergence options. These unconfirmed options must not be treated
as executable iw3 arguments until the bundled CLI is verified, for example with
`python -m iw3 -h` during engine-bundle preparation.

iw3 can download large model files on first run in upstream usage. v3dfy final
offline packaging must avoid first-run downloads by bundling the required
runtime and approved model files before release.

`IW3_CLI_CAPABILITIES.json` is optional metadata for the exact bundled iw3 CLI.
It does not replace `ENGINE_MANIFEST.json`, does not make the engine ready by
itself, and must not enable conversion by itself. When this file is missing,
invalid, or a placeholder, v3dfy must stay conservative and use only the
confirmed base command above.

Example capabilities shape:

```json
{
  "bundledIw3Version": "1.2.3",
  "verifiedBaseCommand": true,
  "verifiedOptions": [],
  "unverifiedOptions": ["selected model", "3D layout", "quality"],
  "verificationSource": "python -m iw3 -h",
  "verifiedAtUtc": "2026-06-04T00:00:00Z",
  "notes": "Record only options verified against the bundled iw3 version."
}
```

Fill this manifest only during engine-bundle preparation after checking the
bundled CLI, for example with `python -m iw3 -h`. The app must not run iw3 to
discover capabilities during normal startup.

These directories currently contain placeholders only. No engine binaries,
models, or runtimes are included yet.

Placeholder files, README files, and contract files are only documentation. They
must not be treated as a prepared engine bundle and must not enable conversion
readiness.

`MODEL_CATALOG.json` is optional metadata for locally bundled models. It must
not download, load, or execute models, and it does not make conversion ready by
itself. Compatible model files still must exist under `engine/iw3/models`.

Example catalog shape:

```json
{
  "models": [
    {
      "id": "depth-default",
      "displayName": "Default depth model",
      "file": "depth-default.onnx",
      "modelType": "depth-estimation",
      "purpose": "2D to 3D depth generation",
      "notes": "Optional local metadata."
    }
  ]
}
```

When the catalog is missing, compatible model files are treated as unmanaged
local models. Invalid or placeholder catalogs are reported for diagnostics but
must not enable model readiness.

Local model selection candidates are derived from compatible files only:
catalog entries with existing compatible files can provide friendly names, and
compatible files not listed in the catalog remain selectable as unmanaged local
models. Building this selection list must not read model binary contents or
load/run model frameworks.

## Directories

- `engine/iw3`: bundled local/offline iw3 engine root.
- `engine/iw3/python`: embedded Python runtime expected to contain
  `python.exe`.
- `engine/iw3/models`: approved pretrained models used by iw3.
- `tools/ffmpeg/win-x64`: bundled `ffmpeg.exe` and `ffprobe.exe`.

## End-user requirement

The final installer must provide every application dependency. End users must
not install Python, FFmpeg, models, the .NET runtime, or a CUDA toolkit
manually.

GPU drivers remain system-level prerequisites and are not packaged with v3dfy.
The application should report unsupported or missing driver conditions clearly.
