# iw3 Engine Placeholder

This directory is reserved for the bundled local/offline iw3 engine.

The final distribution must include the engine, its embedded runtime, models,
configuration, and applicable licenses. Do not rely on globally installed
Python packages.

Expected readiness contract:

- `ENGINE_MANIFEST.json` with a real version, not `placeholder`.
- `python/python.exe` for the embedded Python runtime.
- `iw3.py` or `iw3/__main__.py` as the local iw3 entry file.
- Supported model files under `models`.

This README is documentation only and does not make the engine ready.
