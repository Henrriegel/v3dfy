# Local Engine Layout

## Directories

- `engine/iw3`: bundled local/offline iw3 engine.
- `engine/iw3/python`: embedded Python runtime expected to contain
  `python.exe`.
- `engine/iw3/models`: approved pretrained models used by iw3.
- `tools/ffmpeg/win-x64`: bundled `ffmpeg.exe` and `ffprobe.exe`.

These directories currently contain placeholders only. No engine binaries,
models, or runtimes are included yet.

## End-user requirement

The final installer must provide every application dependency. End users must
not install Python, FFmpeg, models, the .NET runtime, or a CUDA toolkit
manually.

GPU drivers remain system-level prerequisites and are not packaged with v3dfy.
The application should report unsupported or missing driver conditions clearly.
