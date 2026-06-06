# iw3 customization options

This note tracks iw3 options that are safe to expose in v3dfy and options that
must stay internal until verified against the bundled engine.

## Safe user-facing categories now

- Layout flags in the app contract:
  `--half-sbs`, `--half-tb`, and `--anaglyph`.
- `--tb` is kept in the contract for Full Top-Bottom if that layout is added
  later, but Full Top-Bottom is not currently exposed by the UI.
- Depth model mapping through the existing verified mapper. Only mapped local
  model selections are passed to `--depth-model`.
- Optional LG 3D TV 2012 compatibility output as a post-process step after the
  primary iw3 output succeeds. This uses bundled FFmpeg and does not change the
  iw3 bundle layout.

## Keep hidden until separately tested

- Direct Full Side-by-Side output. The current verified contract does not expose
  a direct SBS flag, so the UI must not offer it as a normal layout.
- Direct AVI output from iw3. AVI may only return as a separately modeled legacy
  copy after a successful primary output, and only after the command path is
  validated.
- Video codec, pixel format, quality, scene detection, normalization,
  convergence/divergence, and intensity/depth tuning flags. These can be
  planned as advanced/internal settings but should not become user-facing
  controls until the exact iw3 CLI behavior is verified with the bundled engine.

No Python, iw3, or FFmpeg conversion was run to create this note.
