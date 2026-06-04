# Local Engine Layout

## Bundle contract

The final offline bundle must use this local layout:

```text
engine/iw3/
  ENGINE_MANIFEST.json
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
