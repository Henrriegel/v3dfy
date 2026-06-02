# Architecture

v3dfy is a Windows x64 desktop application built with WPF and .NET 10.
Conversion is local/offline: video files and AI processing must remain on the
user's machine.

## Projects

- `src/V3dfy.App`: WPF user interface and future MVVM composition root.
- `src/V3dfy.Core`: domain models, presets, and platform-independent contracts.
- `src/V3dfy.Infrastructure`: internal path resolution and operating system
  integration.
- `src/V3dfy.Engine.Iw3`: iw3-specific command generation and future execution
  adapter.
- `tests/V3dfy.Tests`: unit tests for domain and adapter behavior.

## Distribution model

The final installer must include the self-contained .NET app, FFmpeg, FFprobe,
the local AI engine, an embedded runtime when required, pretrained models,
configuration files, and applicable licenses. The installed app must not
require cloud APIs or development dependencies.
