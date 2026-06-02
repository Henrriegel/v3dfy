# v3dfy

v3dfy is a Windows offline AI application for local 2D to 3D video conversion.
It is designed as a .NET 10 WPF desktop app with a bundled local AI engine and
no cloud conversion dependency.

The default **General 3D video** preset produces MP4 output with H.264 video and
Half Top-Bottom 3D layout. Device-specific presets, including **LG 3D Full HD
2012**, provide tailored playback guidance. MKV remains available as an
advanced/master output option.

## Current status

The repository currently contains the application scaffold, testable domain
models, selectable output presets, internal tool path resolution, local engine health
checks, iw3 command preview generation, offline distribution placeholders,
support scripts, and initial packaging documentation.

FFmpeg, embedded Python, iw3, pretrained models, and production licenses are not
bundled yet.

## Basic commands

```powershell
.\scripts\check-dev-env.ps1
.\scripts\build.ps1
.\scripts\test.ps1
```

See [docs/setup.md](docs/setup.md) for development setup and
[docs/packaging.md](docs/packaging.md) for the offline packaging direction.
