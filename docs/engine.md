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
