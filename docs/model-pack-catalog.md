# v3dfy Model Pack Catalog

## Purpose

This document records the P3C redistribution audit for the first eight v3dfy
iw3 depth-model candidates. It is documentation only. No checkpoint binaries,
model-pack ZIPs, installers, payloads, or release artifacts were created for
this audit.

The catalog answers which candidates can be packaged as public v3dfy GitHub
Release assets, which candidates must be delayed, and what license/notice files
must be included when a safe pack is eventually built.

## Redistribution Rules Used by v3dfy

v3dfy treats checkpoint/model weights separately from source code. A candidate
is not safe for public redistribution just because the source repository has an
open-source code license.

For a public v3dfy model pack, all of these must be true:

- The exact checkpoint source is official or authoritative.
- The exact checkpoint or repository containing the checkpoint has a clear
  license that permits redistribution.
- Any attribution, citation, model-card, license, or notice obligations can be
  included inside the model pack.
- The pack does not rely on global Python, global FFmpeg, user PATH, first-run
  downloads, online AI services, or hidden installer work.
- The pack contains only approved runtime files and license/notice files, not
  installer caches, developer logs, or unrelated artifacts.

Decision labels:

- `SAFE_FOR_PUBLIC_RELEASE`: official weight source clearly permits
  redistribution with no extra model-pack-specific notice beyond normal source
  provenance.
- `SAFE_WITH_NOTICE`: redistribution appears allowed, but the pack must include
  license, attribution, citation, model-card, or notice files.
- `USER_DOWNLOAD_ONLY`: usable by v3dfy, but v3dfy should not redistribute the
  checkpoint directly.
- `EXCLUDE_NON_COMMERCIAL`: checkpoint/model family is non-commercial,
  research-only, CC-BY-NC, or otherwise unsuitable for public v3dfy release
  assets.
- `BLOCKED_UNCLEAR_LICENSE`: checkpoint source exists, but the exact weight
  license or redistribution permission is unclear.

## Final Decision Summary

| Decision | Candidates |
| --- | --- |
| `SAFE_FOR_PUBLIC_RELEASE` | None. Every currently safe candidate still needs at least license/source/citation files in the pack. |
| `SAFE_WITH_NOTICE` | `depth-anything-metric-indoor`, `depth-anything-metric-outdoor`, `depth-anything-small`, `depth-anything-base`, `depth-anything-v2-small` |
| `USER_DOWNLOAD_ONLY` | None as the final decision. |
| `EXCLUDE_NON_COMMERCIAL` | None among these exact eight candidates. Note: Depth Anything V2 Base/Large/Giant are non-commercial in upstream V2 docs, but only V2 Small is in this P3C set. |
| `BLOCKED_UNCLEAR_LICENSE` | `zoedepth-indoor`, `zoedepth-outdoor`, `zoedepth-indoor-outdoor` |

Safe public GitHub Release assets for this slice are the five
`SAFE_WITH_NOTICE` candidates, provided their model packs include the required
license, source, model-card, and citation files listed below.

The three ZoeDepth checkpoints should not be uploaded by v3dfy until the weight
license is confirmed in writing or an official model card/release page states
that the `.pt` checkpoint weights are redistributable.

## Local and Upstream iw3 Mapping Check

Local repository findings:

- `src/V3dfy.Engine.Iw3/Commands/Iw3DepthModelMapper.cs` contains all eight
  v3dfy IDs, expected relative paths, and iw3 short names.
- `docs/engine.md` and `docs/iw3-bundle-intake.md` document the current engine
  bundle contract and the known default `ZoeD_Any_N` checkpoint path.
- `engine/iw3` currently contains placeholder README files only. A real bundled
  iw3 source tree is not staged in this repository, so local bundled iw3 source
  confirmation is not possible yet beyond the placeholder docs and v3dfy
  registry.

Official upstream iw3 findings:

- Official `nagadomi/nunif` source confirms `ZoeD_N`, `ZoeD_K`, `ZoeD_NK`,
  `ZoeD_Any_N`, and `ZoeD_Any_K` map to the expected `hub/checkpoints/*.pt`
  filenames in `iw3/zoedepth_model.py`.
- Official `nagadomi/nunif` source confirms `Any_S`, `Any_B`, and `Any_V2_S`
  map to the expected `hub/checkpoints/*.pth` filenames in
  `iw3/depth_anything_model.py`.

When a real `engine/iw3/nunif/iw3` bundle is staged, re-check the same short
names against the exact bundled source and `python -m iw3 -h` output before
shipping any executable conversion flow.

## Full Comparison Table

| v3dfy id | Display name | iw3 short name | Expected checkpoint file | Source family | Indoor/outdoor/general | Relative depth or metric depth | Expected speed/weight class | Expected quality/behavior | Best v3dfy use case | Weakness/risk | Official checkpoint source | Weight license | Code license | Redistribution decision | Required files/notices for model pack | Recommended pack name |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `depth-anything-metric-indoor` | Depth Anything Metric Indoor | `ZoeD_Any_N` | `hub/checkpoints/depth_anything_metric_depth_indoor.pt` | Depth Anything v1 metric, ZoeDepth-style metric head | Indoor | Metric depth, NYUv2 indoor fine-tune | Heavy, about 1.34 GB checkpoint, ViT-L class | Strong indoor metric scale; good default for rooms, people indoors, TV/movie interiors | Embedded/base candidate for LG 3D Full HD 2012 after real conversion tests | Large pack; outdoor scale can be wrong; Apache source is clear but pack must include notices | https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints_metric_depth/depth_anything_metric_depth_indoor.pt | Apache-2.0 via official Hugging Face Space metadata for repository contents | Apache-2.0, `LiheYoung/Depth-Anything` | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF Space README/model metadata, source URL, SHA256/source record, Depth Anything citation | `v3dfy-modelpack-depth-anything-metric-indoor-vX.Y.Z.zip` |
| `depth-anything-metric-outdoor` | Depth Anything Metric Outdoor | `ZoeD_Any_K` | `hub/checkpoints/depth_anything_metric_depth_outdoor.pt` | Depth Anything v1 metric, ZoeDepth-style metric head | Outdoor | Metric depth, KITTI outdoor fine-tune | Heavy, about 1.34 GB checkpoint, ViT-L class | Stronger scale for road/outdoor scenes than indoor metric model | First large optional pack for outdoor video after base validation | Large pack; indoor scenes can scale poorly; narrower use case than general relative models | https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints_metric_depth/depth_anything_metric_depth_outdoor.pt | Apache-2.0 via official Hugging Face Space metadata for repository contents | Apache-2.0, `LiheYoung/Depth-Anything` | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF Space README/model metadata, source URL, SHA256/source record, Depth Anything citation | `v3dfy-modelpack-depth-anything-metric-outdoor-vX.Y.Z.zip` |
| `zoedepth-indoor` | ZoeDepth Indoor | `ZoeD_N` | `hub/checkpoints/ZoeD_M12_N.pt` | ZoeDepth official release | Indoor | Metric depth, NYU/indoor single head | Heavy, BEiT-L/MiDaS class | Mature indoor metric depth model; older than Depth Anything metric | Manual import/testing only until license is clarified | Exact weight redistribution permission is unclear; code MIT is not enough | https://github.com/isl-org/ZoeDepth/releases/download/v1.0/ZoeD_M12_N.pt | Unclear for checkpoint weights | MIT code license | `BLOCKED_UNCLEAR_LICENSE` | Do not build public pack. If user imports manually, retain source URL and MIT code license separately from weights. | `v3dfy-modelpack-zoedepth-indoor-vX.Y.Z.zip` only after license clarification |
| `zoedepth-outdoor` | ZoeDepth Outdoor | `ZoeD_K` | `hub/checkpoints/ZoeD_M12_K.pt` | ZoeDepth official release | Outdoor | Metric depth, KITTI/outdoor single head | Heavy, BEiT-L/MiDaS class | Outdoor metric model; can suit driving/outdoor footage | Manual import/testing only until license is clarified | Exact weight redistribution permission is unclear; likely redundant with Depth Anything Metric Outdoor | https://github.com/isl-org/ZoeDepth/releases/download/v1.0/ZoeD_M12_K.pt | Unclear for checkpoint weights | MIT code license | `BLOCKED_UNCLEAR_LICENSE` | Do not build public pack. If user imports manually, retain source URL and MIT code license separately from weights. | `v3dfy-modelpack-zoedepth-outdoor-vX.Y.Z.zip` only after license clarification |
| `zoedepth-indoor-outdoor` | ZoeDepth Indoor Outdoor | `ZoeD_NK` | `hub/checkpoints/ZoeD_M12_NK.pt` | ZoeDepth official release | Indoor/outdoor mixed | Metric depth, dual head with router | Heavy, BEiT-L/MiDaS class, possibly slower/more memory than single-head ZoeDepth | Flexible indoor/outdoor routing; best ZoeDepth family coverage | Best ZoeDepth candidate if license is clarified, but delay for now | Exact weight redistribution permission is unclear; older and likely superseded by Depth Anything metric/V2 options | https://github.com/isl-org/ZoeDepth/releases/download/v1.0/ZoeD_M12_NK.pt | Unclear for checkpoint weights | MIT code license | `BLOCKED_UNCLEAR_LICENSE` | Do not build public pack. If user imports manually, retain source URL and MIT code license separately from weights. | `v3dfy-modelpack-zoedepth-indoor-outdoor-vX.Y.Z.zip` only after license clarification |
| `depth-anything-small` | Depth Anything Small | `Any_S` | `hub/checkpoints/depth_anything_vits14.pth` | Depth Anything v1 relative | General | Relative depth | Lightweight, 24.8M params, about 99.2 MB checkpoint | Fast and robust general relative depth; less metric scale control | Low-VRAM fallback and quick packaging/import validation | Superseded in detail/robustness by Depth Anything V2 Small; relative depth may need stronger normalization | https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints/depth_anything_vits14.pth | Apache-2.0 via official Hugging Face Space metadata for repository contents | Apache-2.0, `LiheYoung/Depth-Anything` | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF Space README/model metadata, source URL, SHA256/source record, Depth Anything citation | `v3dfy-modelpack-depth-anything-small-vX.Y.Z.zip` |
| `depth-anything-base` | Depth Anything Base | `Any_B` | `hub/checkpoints/depth_anything_vitb14.pth` | Depth Anything v1 relative | General | Relative depth | Medium, 97.5M params, about 390 MB checkpoint | Better detail than v1 Small at moderate cost | Quality/performance fallback if V2 Small is not compatible with the bundled iw3 version | Redundant if V2 Small works well; relative depth lacks metric scale | https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints/depth_anything_vitb14.pth | Apache-2.0 via official Hugging Face Space metadata for repository contents | Apache-2.0, `LiheYoung/Depth-Anything` | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF Space README/model metadata, source URL, SHA256/source record, Depth Anything citation | `v3dfy-modelpack-depth-anything-base-vX.Y.Z.zip` |
| `depth-anything-v2-small` | Depth Anything V2 Small | `Any_V2_S` | `hub/checkpoints/depth_anything_v2_vits.pth` | Depth Anything V2 relative | General | Relative depth | Lightweight, 24.8M params, about 99.2 MB checkpoint | Better fine detail and robustness than V1; strong speed/quality balance | Recommended first optional public model pack and lightweight default candidate | Relative depth only; confirm exact bundled iw3 V2 support before making it prominent in UI | https://huggingface.co/depth-anything/Depth-Anything-V2-Small/blob/main/depth_anything_v2_vits.pth | Apache-2.0 for Depth Anything V2 Small model | Apache-2.0, `DepthAnything/Depth-Anything-V2` | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF model card README, source URL, SHA256/source record, Depth Anything V2 and V1 citations | `v3dfy-modelpack-depth-anything-v2-small-vX.Y.Z.zip` |

## Practical Comparison

Best default/base model:

`depth-anything-metric-indoor` remains the best embedded/base candidate because
v3dfy already treats it as the base mapping and it gives metric indoor depth,
which should behave better for many living-room, person, and movie-interior
scenes than a purely relative model. It still needs real conversion validation.

Best first optional model pack:

`depth-anything-v2-small` should be the first optional public pack to build. It
has a clear Apache-2.0 weight license, is small enough for practical release
testing, and should offer the best quality/performance balance among lightweight
general models.

Best indoor model:

`depth-anything-metric-indoor` is the best shippable indoor candidate. The
ZoeDepth indoor model is blocked until its weight license is clarified.

Best outdoor model:

`depth-anything-metric-outdoor` is the best shippable outdoor candidate. It is
large, so it should follow after the lightweight optional pack and after
outdoor conversion tests.

Best lightweight model:

`depth-anything-v2-small` is the best lightweight candidate. `depth-anything-small`
is still safe with notice, but it is likely redundant if V2 Small works with the
bundled iw3 version.

Best quality/performance balance:

`depth-anything-v2-small` is the best initial balance because it is modern,
small, safe with notice, and general-purpose. `depth-anything-base` may be worth
testing if V2 support is unstable or if v1 Base produces more stable stereo
depth in iw3.

Best "do not ship yet" candidate:

`zoedepth-indoor-outdoor` is the most tempting delayed candidate because it
covers both indoor and outdoor scenes, but it is blocked by unclear checkpoint
weight license terms.

Likely redundant models:

- `zoedepth-indoor` is likely redundant with `depth-anything-metric-indoor`.
- `zoedepth-outdoor` is likely redundant with `depth-anything-metric-outdoor`.
- `depth-anything-small` is likely redundant with `depth-anything-v2-small`.
- `depth-anything-base` may be delayed if V2 Small gives sufficient output
  quality in real v3dfy conversions.

Models to test first in real conversion:

1. `depth-anything-metric-indoor`, because it is the embedded/base candidate.
2. `depth-anything-v2-small`, because it is the best first optional pack.
3. `depth-anything-metric-outdoor`, because it complements the indoor base.
4. `depth-anything-base`, only if V2 Small has visible stereo artifacts or
   quality gaps.

Models to delay even if license is safe:

- `depth-anything-metric-outdoor`, because it is large and outdoor-specific.
- `depth-anything-base`, because it is probably secondary to V2 Small.
- `depth-anything-small`, because V2 Small likely supersedes it.

## Per-Model Evidence

### depth-anything-metric-indoor

- Official source name: LiheYoung/Depth-Anything Hugging Face Space,
  `checkpoints_metric_depth/depth_anything_metric_depth_indoor.pt`.
- Official source URL: https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints_metric_depth/depth_anything_metric_depth_indoor.pt
- Exact checkpoint filename: `depth_anything_metric_depth_indoor.pt`.
- Exact v3dfy expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_metric_depth_indoor.pt`.
- Exact iw3 argument: `--depth-model ZoeD_Any_N`.
- Local evidence: v3dfy registry maps this ID to `ZoeD_Any_N`; local docs also
  document this as the current verified default mapping.
- Upstream iw3 evidence: official `nagadomi/nunif` source maps `ZoeD_Any_N` to
  `hub/checkpoints/depth_anything_metric_depth_indoor.pt`.
- Upstream model evidence: official Depth Anything metric-depth README provides
  this exact filename for indoor metric-depth evaluation and points to the
  official Hugging Face Space checkpoint directory.
- License conclusion: Apache-2.0 applies to the official Space repository
  containing the checkpoint, and the upstream code is Apache-2.0.
- Code/weight license difference: no conflict found for this checkpoint source;
  still include the repository/model metadata because the weight license is
  established through the Hugging Face Space metadata, not merely the code file.
- Usage restrictions: no non-commercial or research-only restriction found for
  this exact source; training dataset terms remain relevant only to datasets,
  which v3dfy is not redistributing.
- Redistribution decision: `SAFE_WITH_NOTICE`.
- Model-pack obligations: include Apache-2.0 license, model/source metadata,
  official source URL, source checksum record, and Depth Anything citation.

### depth-anything-metric-outdoor

- Official source name: LiheYoung/Depth-Anything Hugging Face Space,
  `checkpoints_metric_depth/depth_anything_metric_depth_outdoor.pt`.
- Official source URL: https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints_metric_depth/depth_anything_metric_depth_outdoor.pt
- Exact checkpoint filename: `depth_anything_metric_depth_outdoor.pt`.
- Exact v3dfy expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_metric_depth_outdoor.pt`.
- Exact iw3 argument: `--depth-model ZoeD_Any_K`.
- Local evidence: v3dfy registry maps this ID to `ZoeD_Any_K`.
- Upstream iw3 evidence: official `nagadomi/nunif` source maps `ZoeD_Any_K` to
  `hub/checkpoints/depth_anything_metric_depth_outdoor.pt`.
- Upstream model evidence: official Depth Anything metric-depth README provides
  this exact filename for outdoor metric-depth evaluation and points to the
  official Hugging Face Space checkpoint directory.
- License conclusion: Apache-2.0 applies to the official Space repository
  containing the checkpoint, and the upstream code is Apache-2.0.
- Code/weight license difference: no conflict found for this checkpoint source;
  include model metadata to preserve the weight-source license trail.
- Usage restrictions: no non-commercial or research-only restriction found for
  this exact source; outdoor model is trained from KITTI metric-depth data, but
  v3dfy is not redistributing KITTI data.
- Redistribution decision: `SAFE_WITH_NOTICE`.
- Model-pack obligations: include Apache-2.0 license, model/source metadata,
  official source URL, source checksum record, and Depth Anything citation.

### zoedepth-indoor

- Official source name: isl-org/ZoeDepth GitHub Release `v1.0`, asset
  `ZoeD_M12_N.pt`.
- Official source URL: https://github.com/isl-org/ZoeDepth/releases/download/v1.0/ZoeD_M12_N.pt
- Exact checkpoint filename: `ZoeD_M12_N.pt`.
- Exact v3dfy expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/ZoeD_M12_N.pt`.
- Exact iw3 argument: `--depth-model ZoeD_N`.
- Local evidence: v3dfy registry maps this ID to `ZoeD_N`.
- Upstream iw3 evidence: official `nagadomi/nunif` source maps `ZoeD_N` to
  `hub/checkpoints/ZoeD_M12_N.pt`.
- Upstream model evidence: official ZoeDepth README lists `ZoeD_N` torch-hub
  usage; official `hubconf.py` points pretrained loading to the GitHub Release
  asset URL above.
- License conclusion: upstream source code is MIT, but no official source
  checked during P3C clearly states that the `ZoeD_M12_N.pt` checkpoint weights
  are MIT or otherwise redistributable by third parties.
- Code/weight license difference: code is MIT; weight license is unclear.
- Usage restrictions: no explicit non-commercial label found, but absence of an
  exact weight redistribution grant blocks public v3dfy release assets.
- Redistribution decision: `BLOCKED_UNCLEAR_LICENSE`.
- Model-pack obligations: do not build a public pack. Require official weight
  license clarification first.

### zoedepth-outdoor

- Official source name: isl-org/ZoeDepth GitHub Release `v1.0`, asset
  `ZoeD_M12_K.pt`.
- Official source URL: https://github.com/isl-org/ZoeDepth/releases/download/v1.0/ZoeD_M12_K.pt
- Exact checkpoint filename: `ZoeD_M12_K.pt`.
- Exact v3dfy expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/ZoeD_M12_K.pt`.
- Exact iw3 argument: `--depth-model ZoeD_K`.
- Local evidence: v3dfy registry maps this ID to `ZoeD_K`.
- Upstream iw3 evidence: official `nagadomi/nunif` source maps `ZoeD_K` to
  `hub/checkpoints/ZoeD_M12_K.pt`.
- Upstream model evidence: official ZoeDepth README lists `ZoeD_K` torch-hub
  usage; official `hubconf.py` points pretrained loading to the GitHub Release
  asset URL above.
- License conclusion: upstream source code is MIT, but no official source
  checked during P3C clearly states that the `ZoeD_M12_K.pt` checkpoint weights
  are MIT or otherwise redistributable by third parties.
- Code/weight license difference: code is MIT; weight license is unclear.
- Usage restrictions: no explicit non-commercial label found, but absence of an
  exact weight redistribution grant blocks public v3dfy release assets.
- Redistribution decision: `BLOCKED_UNCLEAR_LICENSE`.
- Model-pack obligations: do not build a public pack. Require official weight
  license clarification first.

### zoedepth-indoor-outdoor

- Official source name: isl-org/ZoeDepth GitHub Release `v1.0`, asset
  `ZoeD_M12_NK.pt`.
- Official source URL: https://github.com/isl-org/ZoeDepth/releases/download/v1.0/ZoeD_M12_NK.pt
- Exact checkpoint filename: `ZoeD_M12_NK.pt`.
- Exact v3dfy expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/ZoeD_M12_NK.pt`.
- Exact iw3 argument: `--depth-model ZoeD_NK`.
- Local evidence: v3dfy registry maps this ID to `ZoeD_NK`.
- Upstream iw3 evidence: official `nagadomi/nunif` source maps `ZoeD_NK` to
  `hub/checkpoints/ZoeD_M12_NK.pt`.
- Upstream model evidence: official ZoeDepth README lists `ZoeD_NK` torch-hub
  usage; official `hubconf.py` points pretrained loading to the GitHub Release
  asset URL above.
- License conclusion: upstream source code is MIT, but no official source
  checked during P3C clearly states that the `ZoeD_M12_NK.pt` checkpoint
  weights are MIT or otherwise redistributable by third parties.
- Code/weight license difference: code is MIT; weight license is unclear.
- Usage restrictions: no explicit non-commercial label found, but absence of an
  exact weight redistribution grant blocks public v3dfy release assets.
- Redistribution decision: `BLOCKED_UNCLEAR_LICENSE`.
- Model-pack obligations: do not build a public pack. Require official weight
  license clarification first.

### depth-anything-small

- Official source name: LiheYoung/Depth-Anything Hugging Face Space,
  `checkpoints/depth_anything_vits14.pth`.
- Official source URL: https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints/depth_anything_vits14.pth
- Exact checkpoint filename: `depth_anything_vits14.pth`.
- Exact v3dfy expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_vits14.pth`.
- Exact iw3 argument: `--depth-model Any_S`.
- Local evidence: v3dfy registry maps this ID to `Any_S`.
- Upstream iw3 evidence: official `nagadomi/nunif` source maps `Any_S` to
  `hub/checkpoints/depth_anything_vits14.pth`.
- Upstream model evidence: official Depth Anything README lists the Small model
  as a 24.8M parameter relative-depth model and documents manual download of
  this exact checkpoint filename.
- License conclusion: Apache-2.0 applies to the official Space repository
  containing the checkpoint, and the upstream code is Apache-2.0.
- Code/weight license difference: no conflict found for this checkpoint source;
  include model metadata to preserve the weight-source license trail.
- Usage restrictions: no non-commercial or research-only restriction found for
  this exact source.
- Redistribution decision: `SAFE_WITH_NOTICE`.
- Model-pack obligations: include Apache-2.0 license, model/source metadata,
  official source URL, source checksum record, and Depth Anything citation.

### depth-anything-base

- Official source name: LiheYoung/Depth-Anything Hugging Face Space,
  `checkpoints/depth_anything_vitb14.pth`.
- Official source URL: https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints/depth_anything_vitb14.pth
- Exact checkpoint filename: `depth_anything_vitb14.pth`.
- Exact v3dfy expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_vitb14.pth`.
- Exact iw3 argument: `--depth-model Any_B`.
- Local evidence: v3dfy registry maps this ID to `Any_B`.
- Upstream iw3 evidence: official `nagadomi/nunif` source maps `Any_B` to
  `hub/checkpoints/depth_anything_vitb14.pth`.
- Upstream model evidence: official Depth Anything README lists the Base model
  as a 97.5M parameter relative-depth model and documents manual download of
  this exact checkpoint filename.
- License conclusion: Apache-2.0 applies to the official Space repository
  containing the checkpoint, and the upstream code is Apache-2.0.
- Code/weight license difference: no conflict found for this checkpoint source;
  include model metadata to preserve the weight-source license trail.
- Usage restrictions: no non-commercial or research-only restriction found for
  this exact source.
- Redistribution decision: `SAFE_WITH_NOTICE`.
- Model-pack obligations: include Apache-2.0 license, model/source metadata,
  official source URL, source checksum record, and Depth Anything citation.

### depth-anything-v2-small

- Official source name: depth-anything/Depth-Anything-V2-Small Hugging Face
  model, `depth_anything_v2_vits.pth`.
- Official source URL: https://huggingface.co/depth-anything/Depth-Anything-V2-Small/blob/main/depth_anything_v2_vits.pth
- Exact checkpoint filename: `depth_anything_v2_vits.pth`.
- Exact v3dfy expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_v2_vits.pth`.
- Exact iw3 argument: `--depth-model Any_V2_S`.
- Local evidence: v3dfy registry maps this ID to `Any_V2_S`.
- Upstream iw3 evidence: official `nagadomi/nunif` source maps `Any_V2_S` to
  `hub/checkpoints/depth_anything_v2_vits.pth`.
- Upstream model evidence: official Depth Anything V2 README lists this exact
  checkpoint for the Small model, and the official Hugging Face model card
  lists Apache-2.0 and the exact filename.
- License conclusion: Apache-2.0 applies to Depth Anything V2 Small weights.
  Upstream explicitly separates V2 Small as Apache-2.0 from V2 Base/Large/Giant,
  which are CC-BY-NC-4.0.
- Code/weight license difference: no conflict for V2 Small; do not generalize
  this conclusion to larger V2 models.
- Usage restrictions: no non-commercial or research-only restriction found for
  V2 Small. Larger V2 models are not safe for commercial/public release assets
  unless separately audited.
- Redistribution decision: `SAFE_WITH_NOTICE`.
- Model-pack obligations: include Apache-2.0 license, HF model card README,
  official source URL, source checksum record, and Depth Anything V2/V1
  citations.

## Required License and Notice Files per Safe Pack

Every safe model pack should include a `licenses/models/<pack-id>/` folder in
the ZIP manifest. Recommended files:

### Depth Anything v1 metric and relative packs

Applies to:

- `depth-anything-metric-indoor`
- `depth-anything-metric-outdoor`
- `depth-anything-small`
- `depth-anything-base`

Required files:

- `licenses/models/<pack-id>/LICENSE-Depth-Anything-Apache-2.0.txt`
  copied from the official `LiheYoung/Depth-Anything` license.
- `licenses/models/<pack-id>/MODEL_CARD-OR-SOURCE.md` containing the official
  Hugging Face Space README/model metadata, or a compact generated source file
  that records the Space metadata license, official source URL, authors, model
  family, and checkpoint filename.
- `licenses/models/<pack-id>/SOURCE.txt` with exact checkpoint URL, source
  family, upstream repository URL, retrieval date, expected install path, and
  checksum after the real file is downloaded during a later approved step.
- `licenses/models/<pack-id>/CITATION.txt` with the Depth Anything paper
  citation.
- `licenses/models/<pack-id>/NOTICE.txt` if v3dfy adds attribution text beyond
  the license/source/citation files.

### Depth Anything V2 Small pack

Applies to:

- `depth-anything-v2-small`

Required files:

- `licenses/models/depth-anything-v2-small/LICENSE-Depth-Anything-V2-Apache-2.0.txt`
  copied from the official `DepthAnything/Depth-Anything-V2` license.
- `licenses/models/depth-anything-v2-small/MODEL_CARD.md` copied or summarized
  from the official Hugging Face model card metadata for
  `depth-anything/Depth-Anything-V2-Small`.
- `licenses/models/depth-anything-v2-small/SOURCE.txt` with exact checkpoint
  URL, source family, upstream repository URL, retrieval date, expected install
  path, and checksum after the real file is downloaded during a later approved
  step.
- `licenses/models/depth-anything-v2-small/CITATION.txt` with the Depth
  Anything V2 citation and the Depth Anything V1 citation listed by upstream.
- `licenses/models/depth-anything-v2-small/NOTICE.txt` if v3dfy adds
  attribution text beyond the license/source/citation files.

### ZoeDepth packs

No public v3dfy model pack should be built yet for:

- `zoedepth-indoor`
- `zoedepth-outdoor`
- `zoedepth-indoor-outdoor`

If these remain importable for advanced users, v3dfy documentation can point
users to the official upstream source and record the MIT code license, but
v3dfy should not mirror or upload the checkpoint weights until the weight
license is clarified.

## Recommended First Pack to Build

Build `v3dfy-modelpack-depth-anything-v2-small-vX.Y.Z.zip` first, after a
separate approved implementation slice.

Reasons:

- It is safe with notice under Apache-2.0.
- It is small enough to validate the model-pack manifest, import, checksum,
  license layout, and GitHub Release asset workflow without a multi-GB payload.
- It is likely the best lightweight quality/performance option.
- It gives v3dfy a general-purpose optional model that complements the embedded
  metric indoor base.

The second pack should be
`v3dfy-modelpack-depth-anything-metric-outdoor-vX.Y.Z.zip` if real conversion
testing shows clear outdoor value and release size is acceptable.

## Models Excluded or Delayed and Why

Excluded from public v3dfy release assets for now:

- `zoedepth-indoor`: exact checkpoint weight license is unclear.
- `zoedepth-outdoor`: exact checkpoint weight license is unclear.
- `zoedepth-indoor-outdoor`: exact checkpoint weight license is unclear.

Delayed even though currently safe with notice:

- `depth-anything-metric-outdoor`: safe, but large and outdoor-specific.
- `depth-anything-base`: safe, but likely secondary if V2 Small works well.
- `depth-anything-small`: safe, but likely superseded by V2 Small.

## Open Questions

- The local repository currently has no real bundled iw3 source tree, only
  placeholders. Reconfirm all eight short names against the exact staged
  `engine/iw3/nunif/iw3` source before release packaging.
- Reconfirm `python -m iw3 -h` against the exact bundled iw3 version before
  treating `--depth-model` as an executable option in final conversion.
- For ZoeDepth, obtain explicit official confirmation that the release
  checkpoint weights are MIT or otherwise redistributable, or keep them out of
  public v3dfy assets.
- After any later approved download, record SHA256 checksums in each pack's
  `SOURCE.txt` and in the model-pack manifest.
- Confirm whether any model pack should include additional upstream NOTICE files
  if upstream adds them after this audit.

## Next Step Recommendation

Do not download models yet. In the next implementation slice, build a manifest
template and license/notice layout for
`v3dfy-modelpack-depth-anything-v2-small-vX.Y.Z.zip`, then run it through the
existing model-pack import tests using placeholder bytes only. Real checkpoint
download, checksum capture, ZIP creation, and GitHub Release upload should be
separate explicitly approved steps.

## Official Sources Checked

- Local `AGENTS.md`.
- Local `src/V3dfy.Engine.Iw3/Commands/Iw3DepthModelMapper.cs`.
- Local `docs/engine.md`.
- Local `docs/iw3-bundle-intake.md`.
- Local `engine/iw3` placeholder README files.
- Official `nagadomi/nunif` repository: https://github.com/nagadomi/nunif
- Official `nagadomi/nunif` iw3 source references:
  - https://github.com/nagadomi/nunif/blob/master/iw3/zoedepth_model.py
  - https://github.com/nagadomi/nunif/blob/master/iw3/depth_anything_model.py
- Official Depth Anything repository: https://github.com/LiheYoung/Depth-Anything
- Official Depth Anything license: https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/LICENSE
- Official Depth Anything README: https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/README.md
- Official Depth Anything metric-depth README: https://raw.githubusercontent.com/LiheYoung/Depth-Anything/main/metric_depth/README.md
- Official Depth Anything Hugging Face Space metadata and checkpoints:
  - https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/README.md
  - https://huggingface.co/spaces/LiheYoung/Depth-Anything/tree/main/checkpoints
  - https://huggingface.co/spaces/LiheYoung/Depth-Anything/tree/main/checkpoints_metric_depth
  - https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints/depth_anything_vits14.pth
  - https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints/depth_anything_vitb14.pth
  - https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints_metric_depth/depth_anything_metric_depth_indoor.pt
  - https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints_metric_depth/depth_anything_metric_depth_outdoor.pt
- Official ZoeDepth repository: https://github.com/isl-org/ZoeDepth
- Official ZoeDepth README: https://raw.githubusercontent.com/isl-org/ZoeDepth/main/README.md
- Official ZoeDepth license: https://raw.githubusercontent.com/isl-org/ZoeDepth/main/LICENSE
- Official ZoeDepth hubconf: https://raw.githubusercontent.com/isl-org/ZoeDepth/main/hubconf.py
- Official ZoeDepth release page: https://github.com/isl-org/ZoeDepth/releases
- Official ZoeDepth checkpoint URLs:
  - https://github.com/isl-org/ZoeDepth/releases/download/v1.0/ZoeD_M12_N.pt
  - https://github.com/isl-org/ZoeDepth/releases/download/v1.0/ZoeD_M12_K.pt
  - https://github.com/isl-org/ZoeDepth/releases/download/v1.0/ZoeD_M12_NK.pt
- Official Depth Anything V2 repository: https://github.com/DepthAnything/Depth-Anything-V2
- Official Depth Anything V2 README: https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/README.md
- Official Depth Anything V2 license: https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/LICENSE
- Official Depth Anything V2 metric-depth README: https://raw.githubusercontent.com/DepthAnything/Depth-Anything-V2/main/metric_depth/README.md
- Official Depth Anything V2 Small model card and checkpoint:
  - https://huggingface.co/depth-anything/Depth-Anything-V2-Small
  - https://huggingface.co/depth-anything/Depth-Anything-V2-Small/blob/main/README.md
  - https://huggingface.co/depth-anything/Depth-Anything-V2-Small/blob/main/depth_anything_v2_vits.pth
