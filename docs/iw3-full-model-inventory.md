# iw3 Full Depth Model Inventory

## Purpose

This document records the P3C-2A upstream iw3 depth-model short-name
inventory. It is an inventory only: it does not approve redistribution, build
model packs, add v3dfy mappings, or classify expanded models for release.

The inventory is based on official `nagadomi/nunif` source at upstream
`master` commit `d23721f1b5f0a4c92c3ee1be013180bf298730c5`, checked on
2026-06-13.

## Methodology

- Read the local v3dfy mapper and existing engine/model-pack docs.
- Read the official upstream iw3 CLI `--depth-model` choices list.
- Read each official upstream iw3 depth-model provider's `MODEL_FILES` and
  related alias/type dictionaries.
- Normalize upstream checkpoint paths under iw3 `pretrained_models` as
  `hub/checkpoints/<file>`, because iw3 sets `HUB_MODEL_DIR` to
  `iw3/pretrained_models/hub`.
- Record source-backed inventory facts only. Later license conclusions belong
  in P3C-2B.

No checkpoint binaries, model ZIPs, installers, payloads, or conversion outputs
were downloaded or generated for this inventory.

## Source Files Checked

Local v3dfy files:

- `AGENTS.md`
- `src/V3dfy.Engine.Iw3/Commands/Iw3DepthModelMapper.cs`
- `docs/model-pack-catalog.md`
- `docs/engine.md`
- `docs/iw3-bundle-intake.md`

Official upstream `nagadomi/nunif` sources:

- https://github.com/nagadomi/nunif
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/README.md
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/utils.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/depth_model_factory.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/hub_dir.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/zoedepth_model.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/depth_anything_model.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/depth_anything_v3_model.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/depth_pro_model.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/video_depth_anything_model.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/video_depth_anything_streaming_model.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/null_depth_model.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/download_models.py
- https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/gui.py

The canonical short-name exposure check is `utils.py:2013-2029`. The provider
dispatch check is `depth_model_factory.py:1-33`.

## Inventory Summary

Total upstream iw3 depth-model short names found: 40.

Provider-backed checkpoint short names: 39.

No-checkpoint dummy short names: 1 (`NULL`).

Counts by family:

- ZoeDepth: 3
- Depth Anything metric via ZoeDepth provider: 2
- Depth Anything v1 relative: 3
- Depth Anything V2 relative: 3
- Depth Anything V2 metric: 8, including 2 compatibility aliases
- Distill Any Depth: 3
- Depth Anything 3 monocular: 2, sharing one checkpoint
- Depth Pro: 2, sharing one checkpoint
- Video Depth Anything: 7, including 1 compatibility alias
- Video Depth Anything stream: 6, sharing checkpoints with non-stream VDA
- NullDepth dummy: 1

## Full Upstream iw3 Depth-Model Inventory

| iw3 short name | family | checkpoint file | expected relative path | shared checkpoint / alias group | model type | indoor/outdoor/general | metric/relative | image/video/stream | mapped by v3dfy | v3dfy id if mapped | source evidence | notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ZoeD_N` | ZoeDepth | `ZoeD_M12_N.pt` | `hub/checkpoints/ZoeD_M12_N.pt` | none | normal separate model | indoor | metric | image/video | Yes | `zoedepth-indoor` | `zoedepth_model.py:12-19`; README model table | P3C-2B must confirm exact checkpoint redistribution terms. |
| `ZoeD_K` | ZoeDepth | `ZoeD_M12_K.pt` | `hub/checkpoints/ZoeD_M12_K.pt` | none | normal separate model | outdoor | metric | image/video | Yes | `zoedepth-outdoor` | `zoedepth_model.py:12-19`; README model table | Outdoor/dashboard-camera tuned per iw3 README; P3C-2B required. |
| `ZoeD_NK` | ZoeDepth | `ZoeD_M12_NK.pt` | `hub/checkpoints/ZoeD_M12_NK.pt` | none | normal separate model | indoor/outdoor | metric | image/video | Yes | `zoedepth-indoor-outdoor` | `zoedepth_model.py:12-19`; README model table | Dual NYUv2/KITTI model per iw3 README; P3C-2B required. |
| `ZoeD_Any_N` | Depth Anything metric via ZoeDepth provider | `depth_anything_metric_depth_indoor.pt` | `hub/checkpoints/depth_anything_metric_depth_indoor.pt` | none | metric variant through ZoeDepth provider | indoor | metric | image/video | Yes | `depth-anything-metric-indoor` | `zoedepth_model.py:12-20`; `utils.py:2013-2029`; README model table | iw3 CLI default is `ZoeD_Any_N`; previous catalog covers this first-eight candidate only. |
| `ZoeD_Any_K` | Depth Anything metric via ZoeDepth provider | `depth_anything_metric_depth_outdoor.pt` | `hub/checkpoints/depth_anything_metric_depth_outdoor.pt` | none | metric variant through ZoeDepth provider | outdoor | metric | image/video | Yes | `depth-anything-metric-outdoor` | `zoedepth_model.py:12-20`; README model table | Outdoor/dashboard-camera tuned per iw3 README; P3C-2B required before packaging. |
| `Any_S` | Depth Anything v1 | `depth_anything_vits14.pth` | `hub/checkpoints/depth_anything_vits14.pth` | none | normal separate model, small | general | relative | image/video | Yes | `depth-anything-small` | `depth_anything_model.py:12-60`; README model table | Small/efficient per iw3 README; first-eight catalog already covers this candidate. |
| `Any_B` | Depth Anything v1 | `depth_anything_vitb14.pth` | `hub/checkpoints/depth_anything_vitb14.pth` | none | normal separate model, base | general | relative | image/video | Yes | `depth-anything-base` | `depth_anything_model.py:12-60`; README model table | Base relative-depth model; first-eight catalog already covers this candidate. |
| `Any_L` | Depth Anything v1 | `depth_anything_vitl14.pth` | `hub/checkpoints/depth_anything_vitl14.pth` | none | normal separate model, large | general | relative | image/video | No |  | `depth_anything_model.py:12-60`; README model table | iw3 README marks this as higher quality but heavier; P3C-2B required. |
| `Any_V2_S` | Depth Anything V2 relative | `depth_anything_v2_vits.pth` | `hub/checkpoints/depth_anything_v2_vits.pth` | none | normal separate model, small | general | relative | image/video | Yes | `depth-anything-v2-small` | `depth_anything_model.py:12-60`; README model table | Depth anti-aliasing supported by iw3 for this short name. |
| `Any_V2_B` | Depth Anything V2 relative | `depth_anything_v2_vitb.pth` | `hub/checkpoints/depth_anything_v2_vitb.pth` | none | normal separate model, base | general | relative | image/video | No |  | `depth_anything_model.py:12-60`; README model table and V2 notice | iw3 README flags non-commercial license risk; P3C-2B must verify exact terms. |
| `Any_V2_L` | Depth Anything V2 relative | `depth_anything_v2_vitl.pth` | `hub/checkpoints/depth_anything_v2_vitl.pth` | none | normal separate model, large | general | relative | image/video | No |  | `depth_anything_model.py:12-60`; README model table and V2 notice | iw3 README flags non-commercial license risk; P3C-2B must verify exact terms. |
| `Any_V2_N` | Depth Anything V2 metric | `depth_anything_v2_metric_hypersim_vitl.pth` | `hub/checkpoints/depth_anything_v2_metric_hypersim_vitl.pth` | alias of `Any_V2_N_L` | compatibility alias, large metric | indoor | metric | image/video | No |  | `depth_anything_model.py:27-54`; `utils.py:2017-2020` | Compatibility alias in source; audit with `Any_V2_N_L`. |
| `Any_V2_K` | Depth Anything V2 metric | `depth_anything_v2_metric_vkitti_vitl.pth` | `hub/checkpoints/depth_anything_v2_metric_vkitti_vitl.pth` | alias of `Any_V2_K_L` | compatibility alias, large metric | outdoor | metric | image/video | No |  | `depth_anything_model.py:27-54`; `utils.py:2017-2020` | Compatibility alias in source; audit with `Any_V2_K_L`. |
| `Any_V2_N_S` | Depth Anything V2 metric | `depth_anything_v2_metric_hypersim_vits.pth` | `hub/checkpoints/depth_anything_v2_metric_hypersim_vits.pth` | none | metric variant, small | indoor | metric | image/video | No |  | `depth_anything_model.py:20-46`; README model table | Hypersim indoor-tuned per iw3 README; P3C-2B required. |
| `Any_V2_N_B` | Depth Anything V2 metric | `depth_anything_v2_metric_hypersim_vitb.pth` | `hub/checkpoints/depth_anything_v2_metric_hypersim_vitb.pth` | none | metric variant, base | indoor | metric | image/video | No |  | `depth_anything_model.py:20-46`; README model table | Hypersim indoor-tuned per iw3 README; P3C-2B required. |
| `Any_V2_N_L` | Depth Anything V2 metric | `depth_anything_v2_metric_hypersim_vitl.pth` | `hub/checkpoints/depth_anything_v2_metric_hypersim_vitl.pth` | shared with alias `Any_V2_N` | metric variant, large | indoor | metric | image/video | No |  | `depth_anything_model.py:20-54`; README model table and V2 notice | iw3 README flags this large metric model in the V2 non-commercial notice. |
| `Any_V2_K_S` | Depth Anything V2 metric | `depth_anything_v2_metric_vkitti_vits.pth` | `hub/checkpoints/depth_anything_v2_metric_vkitti_vits.pth` | none | metric variant, small | outdoor | metric | image/video | No |  | `depth_anything_model.py:23-50`; README model table | VKITTI outdoor/dashboard-camera tuned per iw3 README; P3C-2B required. |
| `Any_V2_K_B` | Depth Anything V2 metric | `depth_anything_v2_metric_vkitti_vitb.pth` | `hub/checkpoints/depth_anything_v2_metric_vkitti_vitb.pth` | none | metric variant, base | outdoor | metric | image/video | No |  | `depth_anything_model.py:23-50`; README model table | VKITTI outdoor/dashboard-camera tuned per iw3 README; P3C-2B required. |
| `Any_V2_K_L` | Depth Anything V2 metric | `depth_anything_v2_metric_vkitti_vitl.pth` | `hub/checkpoints/depth_anything_v2_metric_vkitti_vitl.pth` | shared with alias `Any_V2_K` | metric variant, large | outdoor | metric | image/video | No |  | `depth_anything_model.py:23-54`; README model table and V2 notice | iw3 README flags this large metric model in the V2 non-commercial notice. |
| `Distill_Any_S` | Distill Any Depth | `distill_any_depth_vits.safetensors` | `hub/checkpoints/distill_any_depth_vits.safetensors` | none | normal separate model, small | general/unspecified by iw3 | model-defined; verify | image/video | No |  | `depth_anything_model.py:31-60`; README model table | iw3 source does not expose metric behavior until model load; P3C-2B must verify weights and terms. |
| `Distill_Any_B` | Distill Any Depth | `distill_any_depth_vitb.safetensors` | `hub/checkpoints/distill_any_depth_vitb.safetensors` | none | normal separate model, base | general/unspecified by iw3 | model-defined; verify | image/video | No |  | `depth_anything_model.py:31-60`; README Distill notice | iw3 README notes inherited Depth-Anything-V2 concerns; P3C-2B required. |
| `Distill_Any_L` | Distill Any Depth | `distill_any_depth_vitl.safetensors` | `hub/checkpoints/distill_any_depth_vitl.safetensors` | none | normal separate model, large | general/unspecified by iw3 | model-defined; verify | image/video | No |  | `depth_anything_model.py:31-60`; README Distill notice | iw3 README notes inherited Depth-Anything-V2 concerns; P3C-2B required. |
| `Any_V3_Mono` | Depth Anything 3 monocular | `da3mono-large.safetensors` | `hub/checkpoints/da3mono-large.safetensors` | shared with `Any_V3_Mono_01` | scaler variant, large, max scaler | general | relative | image/video | No |  | `depth_anything_v3_model.py:13-24`; `depth_anything_v3_model.py:125-132`; README model table | Adjusted for SBS/VR devices per iw3 README; P3C-2B required. |
| `Any_V3_Mono_01` | Depth Anything 3 monocular | `da3mono-large.safetensors` | `hub/checkpoints/da3mono-large.safetensors` | shared with `Any_V3_Mono` | scaler variant, large, min-max scaler | general | relative | image/video | No |  | `depth_anything_v3_model.py:13-24`; `depth_anything_v3_model.py:125-132`; README model table | Scaler variant for anaglyph/3D TV per iw3 README; audit once per shared checkpoint. |
| `DepthPro` | Depth Pro | `depth_pro.pt` | `hub/checkpoints/depth_pro.pt` | shared with `DepthPro_S` | resolution variant, full | general | relative/disparity in iw3 | image only | No |  | `depth_pro_model.py:12-19`; `depth_pro_model.py:143-203`; README model table | iw3 forces disparity and reports non-metric; source model is image-only in iw3. |
| `DepthPro_S` | Depth Pro | `depth_pro.pt` | `hub/checkpoints/depth_pro.pt` | shared with `DepthPro` | resolution variant, small/modified | general | relative/disparity in iw3 | image only | No |  | `depth_pro_model.py:12-19`; `depth_pro_model.py:143-203`; README model table | Same checkpoint as `DepthPro`; smaller modified resolution per iw3 README. |
| `VDA_S` | Video Depth Anything | `video_depth_anything_vits.pth` | `hub/checkpoints/video_depth_anything_vits.pth` | shared with `VDA_Stream_S` | normal video model, small | general | relative | video only | No |  | `video_depth_anything_model.py:15-32`; README model table | Video-oriented provider; not image-supported in iw3 source. |
| `VDA_B` | Video Depth Anything | `video_depth_anything_vitb.pth` | `hub/checkpoints/video_depth_anything_vitb.pth` | shared with `VDA_Stream_B` | normal video model, base | general | relative | video only | No |  | `video_depth_anything_model.py:15-32`; README VDA notice | iw3 README flags non-commercial license risk; shared audit with stream variant. |
| `VDA_L` | Video Depth Anything | `video_depth_anything_vitl.pth` | `hub/checkpoints/video_depth_anything_vitl.pth` | shared with `VDA_Stream_L` | normal video model, large | general | relative | video only | No |  | `video_depth_anything_model.py:15-32`; README VDA notice | iw3 README flags non-commercial license risk; shared audit with stream variant. |
| `VDA_Metric` | Video Depth Anything metric | `metric_video_depth_anything_vitl.pth` | `hub/checkpoints/metric_video_depth_anything_vitl.pth` | alias/shared with `VDA_Metric_L` and `VDA_Stream_Metric_L` | old compatibility alias, large metric checkpoint | general | metric checkpoint, forced disparity in iw3 | video only | No |  | `video_depth_anything_model.py:15-48`; README recommendation | Source comment marks this old-version compatibility; audit with large metric VDA checkpoint. |
| `VDA_Metric_S` | Video Depth Anything metric | `metric_video_depth_anything_vits.pth` | `hub/checkpoints/metric_video_depth_anything_vits.pth` | shared with `VDA_Stream_Metric_S` | metric video model, small | general | metric checkpoint, forced disparity in iw3 | video only | No |  | `video_depth_anything_model.py:15-48`; README model table | Source sets `force_disparity=True`; P3C-2B required. |
| `VDA_Metric_B` | Video Depth Anything metric | `metric_video_depth_anything_vitb.pth` | `hub/checkpoints/metric_video_depth_anything_vitb.pth` | shared with `VDA_Stream_Metric_B` | metric video model, base | general | metric checkpoint, forced disparity in iw3 | video only | No |  | `video_depth_anything_model.py:15-48`; README VDA notice | iw3 README flags non-commercial license risk; shared audit with stream variant. |
| `VDA_Metric_L` | Video Depth Anything metric | `metric_video_depth_anything_vitl.pth` | `hub/checkpoints/metric_video_depth_anything_vitl.pth` | shared with `VDA_Metric` and `VDA_Stream_Metric_L` | metric video model, large | general | metric checkpoint, forced disparity in iw3 | video only | No |  | `video_depth_anything_model.py:15-48`; README VDA notice | iw3 README flags non-commercial license risk; shared audit with alias and stream variant. |
| `VDA_Stream_S` | Video Depth Anything stream | `video_depth_anything_vits.pth` | `hub/checkpoints/video_depth_anything_vits.pth` | shared with `VDA_S` | stream variant, small | general | relative | video/stream only | No |  | `video_depth_anything_streaming_model.py:12-41`; README VDA stream note | README states stream variants use the same checkpoint files as `VDA_*`. |
| `VDA_Stream_B` | Video Depth Anything stream | `video_depth_anything_vitb.pth` | `hub/checkpoints/video_depth_anything_vitb.pth` | shared with `VDA_B` | stream variant, base | general | relative | video/stream only | No |  | `video_depth_anything_streaming_model.py:12-41`; README VDA stream note | Shares checkpoint with a README-flagged non-commercial-risk model. |
| `VDA_Stream_L` | Video Depth Anything stream | `video_depth_anything_vitl.pth` | `hub/checkpoints/video_depth_anything_vitl.pth` | shared with `VDA_L` | stream variant, large | general | relative | video/stream only | No |  | `video_depth_anything_streaming_model.py:12-41`; README VDA stream note | Shares checkpoint with a README-flagged non-commercial-risk model. |
| `VDA_Stream_Metric_S` | Video Depth Anything stream metric | `metric_video_depth_anything_vits.pth` | `hub/checkpoints/metric_video_depth_anything_vits.pth` | shared with `VDA_Metric_S` | stream metric variant, small | general | metric checkpoint, forced disparity in iw3 | video/stream only | No |  | `video_depth_anything_streaming_model.py:12-41`; README VDA stream note | Audit once with the non-stream small metric checkpoint. |
| `VDA_Stream_Metric_B` | Video Depth Anything stream metric | `metric_video_depth_anything_vitb.pth` | `hub/checkpoints/metric_video_depth_anything_vitb.pth` | shared with `VDA_Metric_B` | stream metric variant, base | general | metric checkpoint, forced disparity in iw3 | video/stream only | No |  | `video_depth_anything_streaming_model.py:12-41`; README VDA stream note | Shares checkpoint with a README-flagged non-commercial-risk model. |
| `VDA_Stream_Metric_L` | Video Depth Anything stream metric | `metric_video_depth_anything_vitl.pth` | `hub/checkpoints/metric_video_depth_anything_vitl.pth` | shared with `VDA_Metric` and `VDA_Metric_L` | stream metric variant, large | general | metric checkpoint, forced disparity in iw3 | video/stream only | No |  | `video_depth_anything_streaming_model.py:12-41`; README VDA stream note | Shares checkpoint with `VDA_Metric` alias and `VDA_Metric_L`. |
| `NULL` | NullDepth dummy | none | none | no checkpoint | dummy benchmark provider | general | relative/dummy grayscale | image/video | No |  | `null_depth_model.py:60-70`; `utils.py:2028` | No model pack target; keep out of end-user selectable model inventory unless explicitly needed for diagnostics. |

## Grouped Family Sections

### ZoeDepth and Depth Anything Metric via ZoeDepth Provider

`zoedepth_model.py` defines five checkpoint-backed short names:
`ZoeD_N`, `ZoeD_K`, `ZoeD_NK`, `ZoeD_Any_N`, and `ZoeD_Any_K`.
All report metric depth through iw3. The two `ZoeD_Any_*` entries use the
Depth Anything metric-depth backbone but are exposed by the ZoeDepth provider.

### Depth Anything Provider

`depth_anything_model.py` defines 17 short names:
`Any_S`, `Any_B`, `Any_L`, `Any_V2_S`, `Any_V2_B`, `Any_V2_L`,
`Any_V2_N_S`, `Any_V2_N_B`, `Any_V2_N_L`, `Any_V2_K_S`,
`Any_V2_K_B`, `Any_V2_K_L`, `Any_V2_N`, `Any_V2_K`,
`Distill_Any_S`, `Distill_Any_B`, and `Distill_Any_L`.

`Any_V2_N` and `Any_V2_K` are compatibility aliases that share the large
metric checkpoints with `Any_V2_N_L` and `Any_V2_K_L`.

### Depth Anything 3

`depth_anything_v3_model.py` defines `Any_V3_Mono` and
`Any_V3_Mono_01`. They share `da3mono-large.safetensors` and differ by iw3
scaler behavior.

### Depth Pro

`depth_pro_model.py` defines `DepthPro` and `DepthPro_S`. They share
`depth_pro.pt` and differ by modified resolution. iw3 marks this provider as
image-only and forces disparity behavior.

### Video Depth Anything

`video_depth_anything_model.py` defines seven short names:
`VDA_S`, `VDA_B`, `VDA_L`, `VDA_Metric`, `VDA_Metric_S`,
`VDA_Metric_B`, and `VDA_Metric_L`. `VDA_Metric` is an old compatibility
alias for the large metric checkpoint.

### Video Depth Anything Stream

`video_depth_anything_streaming_model.py` defines six stream variants:
`VDA_Stream_S`, `VDA_Stream_B`, `VDA_Stream_L`,
`VDA_Stream_Metric_S`, `VDA_Stream_Metric_B`, and
`VDA_Stream_Metric_L`. The iw3 README states that stream variants use the same
checkpoint files as the matching non-stream `VDA_*` models.

### NullDepth

`null_depth_model.py` defines `NULL`, a dummy provider with no checkpoint file.
It is present in the CLI choices list, but it is not a checkpoint/model-pack
candidate.

## Models Already Mapped by v3dfy

The current local mapper covers 8 of the 40 upstream iw3 short names:

| iw3 short name | current v3dfy id | expected relative path |
| --- | --- | --- |
| `ZoeD_Any_N` | `depth-anything-metric-indoor` | `hub/checkpoints/depth_anything_metric_depth_indoor.pt` |
| `ZoeD_Any_K` | `depth-anything-metric-outdoor` | `hub/checkpoints/depth_anything_metric_depth_outdoor.pt` |
| `ZoeD_N` | `zoedepth-indoor` | `hub/checkpoints/ZoeD_M12_N.pt` |
| `ZoeD_K` | `zoedepth-outdoor` | `hub/checkpoints/ZoeD_M12_K.pt` |
| `ZoeD_NK` | `zoedepth-indoor-outdoor` | `hub/checkpoints/ZoeD_M12_NK.pt` |
| `Any_S` | `depth-anything-small` | `hub/checkpoints/depth_anything_vits14.pth` |
| `Any_B` | `depth-anything-base` | `hub/checkpoints/depth_anything_vitb14.pth` |
| `Any_V2_S` | `depth-anything-v2-small` | `hub/checkpoints/depth_anything_v2_vits.pth` |

## Models Not Mapped by v3dfy

The current local mapper does not map these 32 upstream iw3 short names:

`Any_L`, `Any_V2_B`, `Any_V2_L`, `Any_V2_N`, `Any_V2_K`,
`Any_V2_N_S`, `Any_V2_N_B`, `Any_V2_N_L`, `Any_V2_K_S`,
`Any_V2_K_B`, `Any_V2_K_L`, `Distill_Any_S`, `Distill_Any_B`,
`Distill_Any_L`, `Any_V3_Mono`, `Any_V3_Mono_01`, `DepthPro`,
`DepthPro_S`, `VDA_S`, `VDA_B`, `VDA_L`, `VDA_Metric`,
`VDA_Metric_S`, `VDA_Metric_B`, `VDA_Metric_L`, `VDA_Stream_S`,
`VDA_Stream_B`, `VDA_Stream_L`, `VDA_Stream_Metric_S`,
`VDA_Stream_Metric_B`, `VDA_Stream_Metric_L`, and `NULL`.

## Shared Checkpoint and Alias Notes

- `Any_V2_N` shares `depth_anything_v2_metric_hypersim_vitl.pth` with
  `Any_V2_N_L`; source marks `Any_V2_N` as compatibility.
- `Any_V2_K` shares `depth_anything_v2_metric_vkitti_vitl.pth` with
  `Any_V2_K_L`; source marks `Any_V2_K` as compatibility.
- `Any_V3_Mono` and `Any_V3_Mono_01` share `da3mono-large.safetensors` and
  differ by scaler behavior.
- `DepthPro` and `DepthPro_S` share `depth_pro.pt` and differ by modified
  resolution.
- `VDA_Metric`, `VDA_Metric_L`, and `VDA_Stream_Metric_L` share
  `metric_video_depth_anything_vitl.pth`; source marks `VDA_Metric` as old
  compatibility.
- `VDA_Stream_S`, `VDA_Stream_B`, and `VDA_Stream_L` share checkpoint files
  with `VDA_S`, `VDA_B`, and `VDA_L`.
- `VDA_Stream_Metric_S`, `VDA_Stream_Metric_B`, and `VDA_Stream_Metric_L`
  share checkpoint files with `VDA_Metric_S`, `VDA_Metric_B`, and
  `VDA_Metric_L`.
- `NULL` has no checkpoint and no model-pack target.

## Items That Require P3C-2B License Audit

Every checkpoint-backed short name in this file requires P3C-2B review before
v3dfy maps it, packages it, or publishes it as a model pack. The audit should
work by unique checkpoint file first, then propagate the result to aliases and
stream/scaler variants that share the same file.

Priority items called out by the upstream iw3 README:

- `Any_V2_B`, `Any_V2_L`, `Any_V2_N_L`, and `Any_V2_K_L` are explicitly
  discussed in the iw3 V2 non-commercial notice.
- `VDA_B`, `VDA_L`, `VDA_Metric_B`, and `VDA_Metric_L` are explicitly
  discussed in the iw3 Video Depth Anything non-commercial notice.
- `VDA_Stream_B`, `VDA_Stream_L`, `VDA_Stream_Metric_B`, and
  `VDA_Stream_Metric_L` share checkpoint files with the corresponding
  non-stream models above.
- `Distill_Any_B` and `Distill_Any_L` are called out by the iw3 README as
  needing caution because of Depth Anything V2 initial weights.

This inventory intentionally does not turn those notes into final
redistribution decisions.

## Inventory Uncertainty

The short-name count is complete for official upstream `nagadomi/nunif`
commit `d23721f1b5f0a4c92c3ee1be013180bf298730c5` based on the CLI choices
list and all provider files in `iw3/`.

Remaining uncertainty is not about the short-name inventory itself. It is about:

- exact checkpoint redistribution terms;
- whether Distill Any Depth models should be treated as metric or relative in
  v3dfy without loading the upstream model;
- whether future upstream iw3 commits add, remove, or rename short names;
- whether v3dfy should expose any video-only, image-only, alias, scaler, stream,
  or dummy provider in the app UI after engine-bundle CLI verification.

## Next Step Recommendation

Run P3C-2B as a license/provenance audit by unique checkpoint file. Start with
the 8 models already mapped by v3dfy, then audit shared checkpoint groups so
aliases and stream/scaler variants inherit one documented result per file.

Do not add new selectable v3dfy mappings until the exact bundled iw3 version is
staged and `python -m iw3 -h` has been verified during bundle preparation.
