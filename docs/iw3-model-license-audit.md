# iw3 Model License and Provenance Audit

## Purpose

This document records the P3C-2B license/provenance audit for the closed iw3
depth-model inventory in `docs/iw3-full-model-inventory.md`.

The audit classifies redistribution safety by unique checkpoint file, then
propagates each decision to every iw3 short name that uses that checkpoint.
It is documentation only. No checkpoint binaries, model-pack ZIPs, installers,
payloads, releases, or conversion outputs were downloaded or generated.

## Methodology

- Used `docs/iw3-full-model-inventory.md` as the closed model-name inventory.
- Audited the 28 unique checkpoint files from that inventory plus `NULL`.
- Checked official or authoritative model-source pages only: upstream GitHub
  repositories, official Hugging Face model cards or Space metadata, official
  release pages, official license/readme files, and official `nagadomi/nunif`
  iw3 README warnings.
- Treated model/checkpoint weights separately from source code. A permissive
  source-code license alone is not enough for v3dfy model-pack redistribution.
- When official evidence conflicts, used the more conservative decision for
  public v3dfy GitHub Release assets.

## Decision Labels

- `SAFE_FOR_PUBLIC_RELEASE`: official weight source clearly permits
  redistribution and no model-pack-specific files are required beyond normal
  license inclusion.
- `SAFE_WITH_NOTICE`: redistribution appears allowed, but the pack must include
  license, attribution, citation, model-card/source, checksum, or notice files.
- `USER_DOWNLOAD_ONLY`: usable by v3dfy, but v3dfy should not redistribute the
  checkpoint directly.
- `EXCLUDE_NON_COMMERCIAL`: official source marks the checkpoint or relevant
  model family as non-commercial, CC-BY-NC, research-only, or otherwise
  unsuitable for public v3dfy release assets.
- `BLOCKED_UNCLEAR_LICENSE`: checkpoint source exists, but exact weight license
  or redistribution permission is unclear.
- `NOT_A_MODEL_PACK_TARGET`: no checkpoint exists for this iw3 short name.

## Source List

Local v3dfy sources:

- `AGENTS.md`
- `docs/iw3-full-model-inventory.md`
- `docs/model-pack-catalog.md`

Official iw3 sources:

- `nagadomi/nunif` repository:
  https://github.com/nagadomi/nunif
- iw3 README at inventory commit:
  https://github.com/nagadomi/nunif/blob/d23721f1b5f0a4c92c3ee1be013180bf298730c5/iw3/README.md

Official model sources:

- ZoeDepth repository: https://github.com/isl-org/ZoeDepth
- ZoeDepth release `v1.0`: https://github.com/isl-org/ZoeDepth/releases/tag/v1.0
- Depth Anything repository: https://github.com/LiheYoung/Depth-Anything
- Depth Anything HF Space: https://huggingface.co/spaces/LiheYoung/Depth-Anything
- Depth Anything V2 repository: https://github.com/DepthAnything/Depth-Anything-V2
- Depth Anything V2 HF organization models: https://huggingface.co/depth-anything
- Distill Any Depth repository:
  https://github.com/Westlake-AGI-Lab/Distill-Any-Depth
- Distill Any Depth HF model:
  https://huggingface.co/xingyang1/Distill-Any-Depth
- Depth Anything 3 repository:
  https://github.com/ByteDance-Seed/Depth-Anything-3
- DA3MONO-LARGE HF model:
  https://huggingface.co/depth-anything/DA3MONO-LARGE
- Apple Depth Pro repository: https://github.com/apple/ml-depth-pro
- Apple Depth Pro checkpoint source, from the official download script:
  https://ml-site.cdn-apple.com/models/depth-pro/depth_pro.pt
- Video Depth Anything repository:
  https://github.com/DepthAnything/Video-Depth-Anything
- Video Depth Anything HF organization models:
  https://huggingface.co/depth-anything

## Unique Checkpoint Decision Summary

Unique checkpoint/model-pack targets audited: 29 entries.

| Decision | Count |
| --- | ---: |
| `SAFE_FOR_PUBLIC_RELEASE` | 0 |
| `SAFE_WITH_NOTICE` | 15 |
| `USER_DOWNLOAD_ONLY` | 0 |
| `EXCLUDE_NON_COMMERCIAL` | 10 |
| `BLOCKED_UNCLEAR_LICENSE` | 3 |
| `NOT_A_MODEL_PACK_TARGET` | 1 |

## Full Decision Table By Checkpoint

| checkpoint file | iw3 short names using it | family | official source | weight license | code license | commercial use | redistribution decision | required model-pack files/notices | reason | recommended pack name if safe | priority |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `ZoeD_M12_N.pt` | `ZoeD_N` | ZoeDepth | GitHub release asset in `isl-org/ZoeDepth` `v1.0`; repo README/hubconf point to this asset | unclear for weights | MIT | unclear | `BLOCKED_UNCLEAR_LICENSE` | Do not build a public pack. Keep source URL for manual-user diagnostics only. | Official release provides the `.pt`, but no checked official source states that the checkpoint weights are MIT or otherwise redistributable by third parties. | n/a | Block |
| `ZoeD_M12_K.pt` | `ZoeD_K` | ZoeDepth | GitHub release asset in `isl-org/ZoeDepth` `v1.0`; repo README/hubconf point to this asset | unclear for weights | MIT | unclear | `BLOCKED_UNCLEAR_LICENSE` | Do not build a public pack. Keep source URL for manual-user diagnostics only. | Code is MIT, but exact release-weight redistribution permission is not stated. | n/a | Block |
| `ZoeD_M12_NK.pt` | `ZoeD_NK` | ZoeDepth | GitHub release asset in `isl-org/ZoeDepth` `v1.0`; repo README/hubconf point to this asset | unclear for weights | MIT | unclear | `BLOCKED_UNCLEAR_LICENSE` | Do not build a public pack. Keep source URL for manual-user diagnostics only. | Code is MIT, but exact release-weight redistribution permission is not stated. | n/a | Block |
| `depth_anything_metric_depth_indoor.pt` | `ZoeD_Any_N` | Depth Anything metric | Official `LiheYoung/Depth-Anything` HF Space checkpoint and official metric-depth README | Apache-2.0 via official HF Space metadata | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF Space README or source metadata, `SOURCE.txt`, checksum, citation, optional notice | The official HF Space containing this checkpoint is Apache-2.0 and the upstream README documents the metric checkpoint. | `v3dfy-modelpack-depth-anything-metric-indoor-vX.Y.Z.zip` | High |
| `depth_anything_metric_depth_outdoor.pt` | `ZoeD_Any_K` | Depth Anything metric | Official `LiheYoung/Depth-Anything` HF Space checkpoint and official metric-depth README | Apache-2.0 via official HF Space metadata | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF Space README or source metadata, `SOURCE.txt`, checksum, citation, optional notice | The official HF Space containing this checkpoint is Apache-2.0 and the upstream README documents the metric checkpoint. | `v3dfy-modelpack-depth-anything-metric-outdoor-vX.Y.Z.zip` | High |
| `depth_anything_vits14.pth` | `Any_S` | Depth Anything v1 relative | Official `LiheYoung/Depth-Anything` HF Space checkpoint and official README | Apache-2.0 via official HF Space metadata | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF Space README or source metadata, `SOURCE.txt`, checksum, citation, optional notice | Official HF Space metadata is Apache-2.0 and includes the exact checkpoint. | `v3dfy-modelpack-depth-anything-small-vX.Y.Z.zip` | Medium |
| `depth_anything_vitb14.pth` | `Any_B` | Depth Anything v1 relative | Official `LiheYoung/Depth-Anything` HF Space checkpoint and official README | Apache-2.0 via official HF Space metadata | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF Space README or source metadata, `SOURCE.txt`, checksum, citation, optional notice | Official HF Space metadata is Apache-2.0 and includes the exact checkpoint. | `v3dfy-modelpack-depth-anything-base-vX.Y.Z.zip` | Medium |
| `depth_anything_vitl14.pth` | `Any_L` | Depth Anything v1 relative | Official `LiheYoung/Depth-Anything` HF Space checkpoint and official README | Apache-2.0 via official HF Space metadata | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF Space README or source metadata, `SOURCE.txt`, checksum, citation, optional notice | Official HF Space metadata is Apache-2.0 and includes the exact checkpoint. | `v3dfy-modelpack-depth-anything-large-vX.Y.Z.zip` | Low |
| `depth_anything_v2_vits.pth` | `Any_V2_S` | Depth Anything V2 relative | Official `depth-anything/Depth-Anything-V2-Small` HF model card | Apache-2.0 | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF model card, `SOURCE.txt`, checksum, Depth Anything V2/V1 citations, optional notice | Official model card marks Small as Apache-2.0 and includes the exact checkpoint. | `v3dfy-modelpack-depth-anything-v2-small-vX.Y.Z.zip` | Highest |
| `depth_anything_v2_vitb.pth` | `Any_V2_B` | Depth Anything V2 relative | Official `depth-anything/Depth-Anything-V2-Base` HF model card and iw3 README notice | CC-BY-NC-4.0 | Apache-2.0 | No | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. For manual user import, record upstream source and license warning. | Official model card and iw3 README mark Base as non-commercial. | n/a | Exclude |
| `depth_anything_v2_vitl.pth` | `Any_V2_L` | Depth Anything V2 relative | Official `depth-anything/Depth-Anything-V2-Large` HF model card and iw3 README notice | CC-BY-NC-4.0 | Apache-2.0 | No | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. For manual user import, record upstream source and license warning. | Official model card and iw3 README mark Large as non-commercial. | n/a | Exclude |
| `depth_anything_v2_metric_hypersim_vits.pth` | `Any_V2_N_S` | Depth Anything V2 metric | Official `depth-anything/Depth-Anything-V2-Metric-Hypersim-Small` HF model card | Apache-2.0 | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF model card, `SOURCE.txt`, checksum, Depth Anything V2/V1 citations, optional notice | Official metric-small model card marks the exact checkpoint Apache-2.0. | `v3dfy-modelpack-depth-anything-v2-metric-hypersim-small-vX.Y.Z.zip` | Medium |
| `depth_anything_v2_metric_hypersim_vitb.pth` | `Any_V2_N_B` | Depth Anything V2 metric | Official `depth-anything/Depth-Anything-V2-Metric-Hypersim-Base` HF model card | Apache-2.0 | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF model card, `SOURCE.txt`, checksum, Depth Anything V2/V1 citations, optional notice | Official metric-base model card marks the exact checkpoint Apache-2.0, and iw3 does not list this base metric checkpoint in its V2 non-commercial warning. | `v3dfy-modelpack-depth-anything-v2-metric-hypersim-base-vX.Y.Z.zip` | Low |
| `depth_anything_v2_metric_hypersim_vitl.pth` | `Any_V2_N_L`, `Any_V2_N` | Depth Anything V2 metric | Official `depth-anything/Depth-Anything-V2-Metric-Hypersim-Large` HF model card; iw3 README warning | conflicting: HF card says Apache-2.0; iw3 README says CC-BY-NC-4.0 | Apache-2.0 | No for v3dfy public packs until conflict is resolved | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. Treat as user-placed only if ever supported. | Official iw3 README explicitly marks this iw3 large metric checkpoint non-commercial despite the HF card metadata. v3dfy should not ship it without upstream clarification. | n/a | Exclude |
| `depth_anything_v2_metric_vkitti_vits.pth` | `Any_V2_K_S` | Depth Anything V2 metric | Official `depth-anything/Depth-Anything-V2-Metric-VKITTI-Small` HF model card | Apache-2.0 | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF model card, `SOURCE.txt`, checksum, Depth Anything V2/V1 citations, optional notice | Official metric-small model card marks the exact checkpoint Apache-2.0. | `v3dfy-modelpack-depth-anything-v2-metric-vkitti-small-vX.Y.Z.zip` | Medium |
| `depth_anything_v2_metric_vkitti_vitb.pth` | `Any_V2_K_B` | Depth Anything V2 metric | Official `depth-anything/Depth-Anything-V2-Metric-VKITTI-Base` HF model card | Apache-2.0 | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF model card, `SOURCE.txt`, checksum, Depth Anything V2/V1 citations, optional notice | Official metric-base model card marks the exact checkpoint Apache-2.0, and iw3 does not list this base metric checkpoint in its V2 non-commercial warning. | `v3dfy-modelpack-depth-anything-v2-metric-vkitti-base-vX.Y.Z.zip` | Low |
| `depth_anything_v2_metric_vkitti_vitl.pth` | `Any_V2_K_L`, `Any_V2_K` | Depth Anything V2 metric | Official `depth-anything/Depth-Anything-V2-Metric-VKITTI-Large` HF model card; iw3 README warning | conflicting: HF card says Apache-2.0; iw3 README says CC-BY-NC-4.0 | Apache-2.0 | No for v3dfy public packs until conflict is resolved | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. Treat as user-placed only if ever supported. | Official iw3 README explicitly marks this iw3 large metric checkpoint non-commercial despite the HF card metadata. v3dfy should not ship it without upstream clarification. | n/a | Exclude |
| `distill_any_depth_vits.safetensors` | `Distill_Any_S` | Distill Any Depth | Official `xingyang1/Distill-Any-Depth` HF model `small/model.safetensors`; official project README | Apache-2.0 for HF checkpoint repo | MIT code | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 weight metadata, MIT code license, HF model card, upstream README, `SOURCE.txt`, checksum, Distill Any Depth citation, optional notice | Official HF checkpoint repo is Apache-2.0 and iw3 warning only calls out Distill Base/Large inherited-license concerns. iw3 expects this file to be renamed to `distill_any_depth_vits.safetensors`. | `v3dfy-modelpack-distill-any-depth-small-vX.Y.Z.zip` | Low |
| `distill_any_depth_vitb.safetensors` | `Distill_Any_B` | Distill Any Depth | Official `xingyang1/Distill-Any-Depth` HF model `base/model.safetensors`; iw3 README inherited-license warning | Apache-2.0 metadata, but iw3 warns of Depth Anything V2 NC initialization | MIT code | No for v3dfy public packs | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. Treat as user-placed only if ever supported. | Official iw3 README says Distill Base/Large are stated Apache but use Depth Anything V2 non-commercial initial weights. v3dfy should not ship them. | n/a | Exclude |
| `distill_any_depth_vitl.safetensors` | `Distill_Any_L` | Distill Any Depth | Official `xingyang1/Distill-Any-Depth` HF model `large/model.safetensors`; iw3 README inherited-license warning | Apache-2.0 metadata, but iw3 warns of Depth Anything V2 NC initialization | MIT code | No for v3dfy public packs | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. Treat as user-placed only if ever supported. | Official iw3 README says Distill Base/Large are stated Apache but use Depth Anything V2 non-commercial initial weights. v3dfy should not ship them. | n/a | Exclude |
| `da3mono-large.safetensors` | `Any_V3_Mono`, `Any_V3_Mono_01` | Depth Anything 3 monocular | Official `depth-anything/DA3MONO-LARGE` HF model card; official `ByteDance-Seed/Depth-Anything-3` README | Apache-2.0 | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF model card, upstream README, `SOURCE.txt`, checksum, DA3 citation, optional notice | The DA3 model card marks DA3MONO-LARGE Apache-2.0. iw3's local filename is a renamed form of the official `model.safetensors`. | `v3dfy-modelpack-depth-anything-3-mono-large-vX.Y.Z.zip` | Low |
| `depth_pro.pt` | `DepthPro`, `DepthPro_S` | Depth Pro | Official Apple `ml-depth-pro` README and download script | Apple `ml-depth-pro` LICENSE terms | Apple `ml-depth-pro` LICENSE terms | Allowed by no non-commercial restriction; no patent grant | `SAFE_WITH_NOTICE` | Apple LICENSE, Apple ACKNOWLEDGEMENTS, README/source metadata, `SOURCE.txt`, checksum, Depth Pro citation, trademark/no-endorsement notice | Apple README states the model weights are released under the repository LICENSE; that license permits use, reproduction, modification, and redistribution with required notices. | `v3dfy-modelpack-depth-pro-vX.Y.Z.zip` | Low |
| `video_depth_anything_vits.pth` | `VDA_S`, `VDA_Stream_S` | Video Depth Anything relative | Official `depth-anything/Video-Depth-Anything-Small` HF model card and upstream README | Apache-2.0 | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF model card, upstream README, `SOURCE.txt`, checksum, Video Depth Anything citation, optional notice | Official model card and README mark Small Apache-2.0; stream short name shares the same checkpoint. | `v3dfy-modelpack-video-depth-anything-small-vX.Y.Z.zip` | Medium |
| `video_depth_anything_vitb.pth` | `VDA_B`, `VDA_Stream_B` | Video Depth Anything relative | Official `depth-anything/Video-Depth-Anything-Base` HF model card and upstream README | CC-BY-NC-4.0 | Apache-2.0 | No | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. For manual user import, record upstream source and license warning. | Official model card and README mark Base non-commercial; stream short name shares the same checkpoint. | n/a | Exclude |
| `video_depth_anything_vitl.pth` | `VDA_L`, `VDA_Stream_L` | Video Depth Anything relative | Official `depth-anything/Video-Depth-Anything-Large` HF model card and upstream README | CC-BY-NC-4.0 | Apache-2.0 | No | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. For manual user import, record upstream source and license warning. | Official model card and README mark Large non-commercial; stream short name shares the same checkpoint. | n/a | Exclude |
| `metric_video_depth_anything_vits.pth` | `VDA_Metric_S`, `VDA_Stream_Metric_S` | Video Depth Anything metric | Official `depth-anything/Metric-Video-Depth-Anything-Small` HF model card and upstream README | Apache-2.0 | Apache-2.0 | Yes | `SAFE_WITH_NOTICE` | Apache-2.0 license, HF model card, upstream README, `SOURCE.txt`, checksum, Video Depth Anything citation, optional notice | Official metric-small model card marks the checkpoint Apache-2.0; stream short name shares the same checkpoint. | `v3dfy-modelpack-metric-video-depth-anything-small-vX.Y.Z.zip` | Medium |
| `metric_video_depth_anything_vitb.pth` | `VDA_Metric_B`, `VDA_Stream_Metric_B` | Video Depth Anything metric | Official `depth-anything/Metric-Video-Depth-Anything-Base` HF model card and iw3 README notice | CC-BY-NC-4.0 | Apache-2.0 | No | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. For manual user import, record upstream source and license warning. | Official model card and iw3 README mark metric Base non-commercial; stream short name shares the same checkpoint. | n/a | Exclude |
| `metric_video_depth_anything_vitl.pth` | `VDA_Metric`, `VDA_Metric_L`, `VDA_Stream_Metric_L` | Video Depth Anything metric | Official `depth-anything/Metric-Video-Depth-Anything-Large` HF model card and iw3 README notice | CC-BY-NC-4.0 | Apache-2.0 | No | `EXCLUDE_NON_COMMERCIAL` | Do not redistribute. For manual user import, record upstream source and license warning. | Official model card and iw3 README mark metric Large non-commercial; `VDA_Metric` is an old compatibility alias for this checkpoint. | n/a | Exclude |
| `none` | `NULL` | NullDepth dummy | Official iw3 `null_depth_model.py` and CLI choices | none | MIT for nunif source | n/a | `NOT_A_MODEL_PACK_TARGET` | No pack files. Do not publish a model pack. | `NULL` is a dummy no-checkpoint provider, not a redistributable model. | n/a | n/a |

## Propagated Decision Table By iw3 Short Name

| iw3 short name | checkpoint file | shared group | mapped by v3dfy | v3dfy id if mapped | decision | reason | recommended action |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `ZoeD_N` | `ZoeD_M12_N.pt` | none | Yes | `zoedepth-indoor` | `BLOCKED_UNCLEAR_LICENSE` | ZoeDepth release weight license is unclear. | Keep import-only/diagnostic; do not publish pack. |
| `ZoeD_K` | `ZoeD_M12_K.pt` | none | Yes | `zoedepth-outdoor` | `BLOCKED_UNCLEAR_LICENSE` | ZoeDepth release weight license is unclear. | Keep import-only/diagnostic; do not publish pack. |
| `ZoeD_NK` | `ZoeD_M12_NK.pt` | none | Yes | `zoedepth-indoor-outdoor` | `BLOCKED_UNCLEAR_LICENSE` | ZoeDepth release weight license is unclear. | Keep import-only/diagnostic; do not publish pack. |
| `ZoeD_Any_N` | `depth_anything_metric_depth_indoor.pt` | none | Yes | `depth-anything-metric-indoor` | `SAFE_WITH_NOTICE` | Official HF Space is Apache-2.0. | Keep mapped; candidate embedded/base pack after checksum capture. |
| `ZoeD_Any_K` | `depth_anything_metric_depth_outdoor.pt` | none | Yes | `depth-anything-metric-outdoor` | `SAFE_WITH_NOTICE` | Official HF Space is Apache-2.0. | Keep mapped; optional outdoor pack after validation. |
| `Any_S` | `depth_anything_vits14.pth` | none | Yes | `depth-anything-small` | `SAFE_WITH_NOTICE` | Official HF Space is Apache-2.0. | Keep mapped; lower priority than V2 Small. |
| `Any_B` | `depth_anything_vitb14.pth` | none | Yes | `depth-anything-base` | `SAFE_WITH_NOTICE` | Official HF Space is Apache-2.0. | Keep mapped; optional fallback pack. |
| `Any_L` | `depth_anything_vitl14.pth` | none | No |  | `SAFE_WITH_NOTICE` | Official HF Space is Apache-2.0. | Safe but large/redundant; delay mapping. |
| `Any_V2_S` | `depth_anything_v2_vits.pth` | none | Yes | `depth-anything-v2-small` | `SAFE_WITH_NOTICE` | Official V2 Small card is Apache-2.0. | Best first optional public pack. |
| `Any_V2_B` | `depth_anything_v2_vitb.pth` | none | No |  | `EXCLUDE_NON_COMMERCIAL` | Official model card is CC-BY-NC-4.0. | Do not map for public packs. |
| `Any_V2_L` | `depth_anything_v2_vitl.pth` | none | No |  | `EXCLUDE_NON_COMMERCIAL` | Official model card is CC-BY-NC-4.0. | Do not map for public packs. |
| `Any_V2_N` | `depth_anything_v2_metric_hypersim_vitl.pth` | alias of `Any_V2_N_L` | No |  | `EXCLUDE_NON_COMMERCIAL` | iw3 marks the large metric checkpoint non-commercial. | Do not map for public packs. |
| `Any_V2_K` | `depth_anything_v2_metric_vkitti_vitl.pth` | alias of `Any_V2_K_L` | No |  | `EXCLUDE_NON_COMMERCIAL` | iw3 marks the large metric checkpoint non-commercial. | Do not map for public packs. |
| `Any_V2_N_S` | `depth_anything_v2_metric_hypersim_vits.pth` | none | No |  | `SAFE_WITH_NOTICE` | Official metric-small card is Apache-2.0. | Candidate indoor metric pack after CLI validation. |
| `Any_V2_N_B` | `depth_anything_v2_metric_hypersim_vitb.pth` | none | No |  | `SAFE_WITH_NOTICE` | Official metric-base card is Apache-2.0. | Safe but lower priority due size/redundancy. |
| `Any_V2_N_L` | `depth_anything_v2_metric_hypersim_vitl.pth` | shared with alias `Any_V2_N` | No |  | `EXCLUDE_NON_COMMERCIAL` | iw3 marks this large metric checkpoint non-commercial. | Do not map for public packs. |
| `Any_V2_K_S` | `depth_anything_v2_metric_vkitti_vits.pth` | none | No |  | `SAFE_WITH_NOTICE` | Official metric-small card is Apache-2.0. | Candidate outdoor metric pack after CLI validation. |
| `Any_V2_K_B` | `depth_anything_v2_metric_vkitti_vitb.pth` | none | No |  | `SAFE_WITH_NOTICE` | Official metric-base card is Apache-2.0. | Safe but lower priority due size/redundancy. |
| `Any_V2_K_L` | `depth_anything_v2_metric_vkitti_vitl.pth` | shared with alias `Any_V2_K` | No |  | `EXCLUDE_NON_COMMERCIAL` | iw3 marks this large metric checkpoint non-commercial. | Do not map for public packs. |
| `Distill_Any_S` | `distill_any_depth_vits.safetensors` | none | No |  | `SAFE_WITH_NOTICE` | Official HF checkpoint repo is Apache-2.0; iw3 warning targets B/L. | Safe but lower priority until quality is proven. |
| `Distill_Any_B` | `distill_any_depth_vitb.safetensors` | none | No |  | `EXCLUDE_NON_COMMERCIAL` | iw3 warns B/L use Depth Anything V2 NC initial weights. | Do not map for public packs. |
| `Distill_Any_L` | `distill_any_depth_vitl.safetensors` | none | No |  | `EXCLUDE_NON_COMMERCIAL` | iw3 warns B/L use Depth Anything V2 NC initial weights. | Do not map for public packs. |
| `Any_V3_Mono` | `da3mono-large.safetensors` | shared with `Any_V3_Mono_01` | No |  | `SAFE_WITH_NOTICE` | Official DA3MONO-LARGE card is Apache-2.0. | Safe but large/new; defer mapping until CLI validation. |
| `Any_V3_Mono_01` | `da3mono-large.safetensors` | shared with `Any_V3_Mono` | No |  | `SAFE_WITH_NOTICE` | Same Apache-2.0 DA3MONO-LARGE checkpoint, scaler variant. | Safe but avoid duplicate pack; one pack covers both. |
| `DepthPro` | `depth_pro.pt` | shared with `DepthPro_S` | No |  | `SAFE_WITH_NOTICE` | Apple README says weights use the repo LICENSE, which permits redistribution with notices. | Safe but image-only in iw3; low priority for video workflow. |
| `DepthPro_S` | `depth_pro.pt` | shared with `DepthPro` | No |  | `SAFE_WITH_NOTICE` | Same Apple-licensed checkpoint, resolution variant. | Same pack as `DepthPro`; low priority. |
| `VDA_S` | `video_depth_anything_vits.pth` | shared with `VDA_Stream_S` | No |  | `SAFE_WITH_NOTICE` | Official VDA Small card is Apache-2.0. | Strong candidate after v3dfy supports video-only depth models. |
| `VDA_B` | `video_depth_anything_vitb.pth` | shared with `VDA_Stream_B` | No |  | `EXCLUDE_NON_COMMERCIAL` | Official VDA Base card is CC-BY-NC-4.0. | Do not map for public packs. |
| `VDA_L` | `video_depth_anything_vitl.pth` | shared with `VDA_Stream_L` | No |  | `EXCLUDE_NON_COMMERCIAL` | Official VDA Large card is CC-BY-NC-4.0. | Do not map for public packs. |
| `VDA_Metric` | `metric_video_depth_anything_vitl.pth` | alias/shared with `VDA_Metric_L`, `VDA_Stream_Metric_L` | No |  | `EXCLUDE_NON_COMMERCIAL` | Official metric large card is CC-BY-NC-4.0. | Do not map for public packs. |
| `VDA_Metric_S` | `metric_video_depth_anything_vits.pth` | shared with `VDA_Stream_Metric_S` | No |  | `SAFE_WITH_NOTICE` | Official metric-small card is Apache-2.0. | Strong candidate after video-depth mapping support. |
| `VDA_Metric_B` | `metric_video_depth_anything_vitb.pth` | shared with `VDA_Stream_Metric_B` | No |  | `EXCLUDE_NON_COMMERCIAL` | Official metric base card is CC-BY-NC-4.0. | Do not map for public packs. |
| `VDA_Metric_L` | `metric_video_depth_anything_vitl.pth` | shared with `VDA_Metric`, `VDA_Stream_Metric_L` | No |  | `EXCLUDE_NON_COMMERCIAL` | Official metric large card is CC-BY-NC-4.0. | Do not map for public packs. |
| `VDA_Stream_S` | `video_depth_anything_vits.pth` | shared with `VDA_S` | No |  | `SAFE_WITH_NOTICE` | Stream variant shares Apache-2.0 Small checkpoint. | Same pack as `VDA_S`; map only if stream flow is supported. |
| `VDA_Stream_B` | `video_depth_anything_vitb.pth` | shared with `VDA_B` | No |  | `EXCLUDE_NON_COMMERCIAL` | Stream variant shares CC-BY-NC Base checkpoint. | Do not map for public packs. |
| `VDA_Stream_L` | `video_depth_anything_vitl.pth` | shared with `VDA_L` | No |  | `EXCLUDE_NON_COMMERCIAL` | Stream variant shares CC-BY-NC Large checkpoint. | Do not map for public packs. |
| `VDA_Stream_Metric_S` | `metric_video_depth_anything_vits.pth` | shared with `VDA_Metric_S` | No |  | `SAFE_WITH_NOTICE` | Stream variant shares Apache-2.0 metric-small checkpoint. | Same pack as `VDA_Metric_S`; map only if stream flow is supported. |
| `VDA_Stream_Metric_B` | `metric_video_depth_anything_vitb.pth` | shared with `VDA_Metric_B` | No |  | `EXCLUDE_NON_COMMERCIAL` | Stream variant shares CC-BY-NC metric-base checkpoint. | Do not map for public packs. |
| `VDA_Stream_Metric_L` | `metric_video_depth_anything_vitl.pth` | shared with `VDA_Metric`, `VDA_Metric_L` | No |  | `EXCLUDE_NON_COMMERCIAL` | Stream variant shares CC-BY-NC metric-large checkpoint. | Do not map for public packs. |
| `NULL` | none | no checkpoint | No |  | `NOT_A_MODEL_PACK_TARGET` | Dummy provider only. | No model pack. |

## SAFE_WITH_NOTICE Definitive List

Each item below is safe for a public v3dfy GitHub Release model-pack asset only
if the listed notices and source metadata are included.

### `depth_anything_v2_vits.pth`

- iw3 short names: `Any_V2_S`
- recommended v3dfy id: `depth-anything-v2-small`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_v2_vits.pth`
- official source URL:
  https://huggingface.co/depth-anything/Depth-Anything-V2-Small/blob/main/depth_anything_v2_vits.pth
- required files: Apache-2.0 license, HF model card, `SOURCE.txt`,
  checksum, Depth Anything V2 and V1 citations, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-v2-small-vX.Y.Z.zip`
- why useful: best first lightweight general relative-depth pack; already
  mapped by v3dfy.

### `depth_anything_metric_depth_indoor.pt`

- iw3 short names: `ZoeD_Any_N`
- recommended v3dfy id: `depth-anything-metric-indoor`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_metric_depth_indoor.pt`
- official source URL:
  https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints_metric_depth/depth_anything_metric_depth_indoor.pt
- required files: Apache-2.0 license, HF Space metadata or README,
  `SOURCE.txt`, checksum, Depth Anything citation, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-metric-indoor-vX.Y.Z.zip`
- why useful: mapped default/base candidate with indoor metric depth.

### `depth_anything_metric_depth_outdoor.pt`

- iw3 short names: `ZoeD_Any_K`
- recommended v3dfy id: `depth-anything-metric-outdoor`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_metric_depth_outdoor.pt`
- official source URL:
  https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints_metric_depth/depth_anything_metric_depth_outdoor.pt
- required files: Apache-2.0 license, HF Space metadata or README,
  `SOURCE.txt`, checksum, Depth Anything citation, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-metric-outdoor-vX.Y.Z.zip`
- why useful: mapped outdoor metric complement to the indoor base.

### `depth_anything_vits14.pth`

- iw3 short names: `Any_S`
- recommended v3dfy id: `depth-anything-small`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_vits14.pth`
- official source URL:
  https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints/depth_anything_vits14.pth
- required files: Apache-2.0 license, HF Space metadata or README,
  `SOURCE.txt`, checksum, Depth Anything citation, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-small-vX.Y.Z.zip`
- why useful: mapped lightweight fallback, but likely redundant with V2 Small.

### `depth_anything_vitb14.pth`

- iw3 short names: `Any_B`
- recommended v3dfy id: `depth-anything-base`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_vitb14.pth`
- official source URL:
  https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints/depth_anything_vitb14.pth
- required files: Apache-2.0 license, HF Space metadata or README,
  `SOURCE.txt`, checksum, Depth Anything citation, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-base-vX.Y.Z.zip`
- why useful: mapped v1 base fallback if V2 Small output quality is not enough.

### `depth_anything_vitl14.pth`

- iw3 short names: `Any_L`
- recommended v3dfy id: `depth-anything-large`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_vitl14.pth`
- official source URL:
  https://huggingface.co/spaces/LiheYoung/Depth-Anything/blob/main/checkpoints/depth_anything_vitl14.pth
- required files: Apache-2.0 license, HF Space metadata or README,
  `SOURCE.txt`, checksum, Depth Anything citation, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-large-vX.Y.Z.zip`
- why useful: higher quality v1 relative model, but not currently mapped and
  likely too redundant for an early pack.

### `depth_anything_v2_metric_hypersim_vits.pth`

- iw3 short names: `Any_V2_N_S`
- recommended v3dfy id: `depth-anything-v2-metric-hypersim-small`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_v2_metric_hypersim_vits.pth`
- official source URL:
  https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-Hypersim-Small/blob/main/depth_anything_v2_metric_hypersim_vits.pth
- required files: Apache-2.0 license, HF model card, `SOURCE.txt`,
  checksum, Depth Anything V2/V1 citations, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-v2-metric-hypersim-small-vX.Y.Z.zip`
- why useful: safe small indoor metric V2 candidate, not currently mapped.

### `depth_anything_v2_metric_hypersim_vitb.pth`

- iw3 short names: `Any_V2_N_B`
- recommended v3dfy id: `depth-anything-v2-metric-hypersim-base`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_v2_metric_hypersim_vitb.pth`
- official source URL:
  https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-Hypersim-Base/blob/main/depth_anything_v2_metric_hypersim_vitb.pth
- required files: Apache-2.0 license, HF model card, `SOURCE.txt`,
  checksum, Depth Anything V2/V1 citations, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-v2-metric-hypersim-base-vX.Y.Z.zip`
- why useful: safe indoor metric V2 base option, but lower priority due size.

### `depth_anything_v2_metric_vkitti_vits.pth`

- iw3 short names: `Any_V2_K_S`
- recommended v3dfy id: `depth-anything-v2-metric-vkitti-small`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_v2_metric_vkitti_vits.pth`
- official source URL:
  https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-VKITTI-Small/blob/main/depth_anything_v2_metric_vkitti_vits.pth
- required files: Apache-2.0 license, HF model card, `SOURCE.txt`,
  checksum, Depth Anything V2/V1 citations, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-v2-metric-vkitti-small-vX.Y.Z.zip`
- why useful: safe small outdoor metric V2 candidate, not currently mapped.

### `depth_anything_v2_metric_vkitti_vitb.pth`

- iw3 short names: `Any_V2_K_B`
- recommended v3dfy id: `depth-anything-v2-metric-vkitti-base`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_anything_v2_metric_vkitti_vitb.pth`
- official source URL:
  https://huggingface.co/depth-anything/Depth-Anything-V2-Metric-VKITTI-Base/blob/main/depth_anything_v2_metric_vkitti_vitb.pth
- required files: Apache-2.0 license, HF model card, `SOURCE.txt`,
  checksum, Depth Anything V2/V1 citations, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-v2-metric-vkitti-base-vX.Y.Z.zip`
- why useful: safe outdoor metric V2 base option, but lower priority due size.

### `distill_any_depth_vits.safetensors`

- iw3 short names: `Distill_Any_S`
- recommended v3dfy id: `distill-any-depth-small`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/distill_any_depth_vits.safetensors`
- official source URL:
  https://huggingface.co/xingyang1/Distill-Any-Depth/blob/main/small/model.safetensors
- required files: Apache-2.0 HF checkpoint metadata, MIT code license,
  upstream README, HF model card, `SOURCE.txt`, checksum, Distill Any Depth
  citation, optional notice
- recommended pack name:
  `v3dfy-modelpack-distill-any-depth-small-vX.Y.Z.zip`
- why useful: safe small Distill candidate, but not currently mapped and needs
  output-quality testing.

### `da3mono-large.safetensors`

- iw3 short names: `Any_V3_Mono`, `Any_V3_Mono_01`
- recommended v3dfy id: `depth-anything-3-mono-large`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/da3mono-large.safetensors`
- official source URL:
  https://huggingface.co/depth-anything/DA3MONO-LARGE/blob/main/model.safetensors
- required files: Apache-2.0 license, HF model card, upstream README,
  `SOURCE.txt`, checksum, Depth Anything 3 citation, optional notice
- recommended pack name:
  `v3dfy-modelpack-depth-anything-3-mono-large-vX.Y.Z.zip`
- why useful: modern safe DA3 model, but large and not currently mapped.

### `depth_pro.pt`

- iw3 short names: `DepthPro`, `DepthPro_S`
- recommended v3dfy id: `depth-pro`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/depth_pro.pt`
- official source URL:
  https://ml-site.cdn-apple.com/models/depth-pro/depth_pro.pt
- required files: Apple `ml-depth-pro` LICENSE, Apple ACKNOWLEDGEMENTS,
  README/source metadata, `SOURCE.txt`, checksum, Depth Pro citation,
  trademark/no-endorsement notice
- recommended pack name:
  `v3dfy-modelpack-depth-pro-vX.Y.Z.zip`
- why useful: safe with notices, but iw3 treats it as image-only, so it is low
  priority for v3dfy's video-first workflow.

### `video_depth_anything_vits.pth`

- iw3 short names: `VDA_S`, `VDA_Stream_S`
- recommended v3dfy id: `video-depth-anything-small`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/video_depth_anything_vits.pth`
- official source URL:
  https://huggingface.co/depth-anything/Video-Depth-Anything-Small/blob/main/video_depth_anything_vits.pth
- required files: Apache-2.0 license, HF model card, upstream README,
  `SOURCE.txt`, checksum, Video Depth Anything citation, optional notice
- recommended pack name:
  `v3dfy-modelpack-video-depth-anything-small-vX.Y.Z.zip`
- why useful: safe temporal video-depth model, but v3dfy does not currently map
  video-only depth models.

### `metric_video_depth_anything_vits.pth`

- iw3 short names: `VDA_Metric_S`, `VDA_Stream_Metric_S`
- recommended v3dfy id: `metric-video-depth-anything-small`
- expected install path:
  `engine/iw3/nunif/iw3/pretrained_models/hub/checkpoints/metric_video_depth_anything_vits.pth`
- official source URL:
  https://huggingface.co/depth-anything/Metric-Video-Depth-Anything-Small/blob/main/metric_video_depth_anything_vits.pth
- required files: Apache-2.0 license, HF model card, upstream README,
  `SOURCE.txt`, checksum, Video Depth Anything citation, optional notice
- recommended pack name:
  `v3dfy-modelpack-metric-video-depth-anything-small-vX.Y.Z.zip`
- why useful: safe metric video-depth model, but v3dfy does not currently map
  video-only depth models.

## SAFE_FOR_PUBLIC_RELEASE Definitive List

None. Every redistributable checkpoint still needs at least license, source,
model-card, citation, checksum, or notice files in the pack.

## EXCLUDE_NON_COMMERCIAL List

- `depth_anything_v2_vitb.pth`: `Any_V2_B`
- `depth_anything_v2_vitl.pth`: `Any_V2_L`
- `depth_anything_v2_metric_hypersim_vitl.pth`: `Any_V2_N_L`, `Any_V2_N`
- `depth_anything_v2_metric_vkitti_vitl.pth`: `Any_V2_K_L`, `Any_V2_K`
- `distill_any_depth_vitb.safetensors`: `Distill_Any_B`
- `distill_any_depth_vitl.safetensors`: `Distill_Any_L`
- `video_depth_anything_vitb.pth`: `VDA_B`, `VDA_Stream_B`
- `video_depth_anything_vitl.pth`: `VDA_L`, `VDA_Stream_L`
- `metric_video_depth_anything_vitb.pth`: `VDA_Metric_B`, `VDA_Stream_Metric_B`
- `metric_video_depth_anything_vitl.pth`: `VDA_Metric`, `VDA_Metric_L`,
  `VDA_Stream_Metric_L`

## BLOCKED_UNCLEAR_LICENSE List

- `ZoeD_M12_N.pt`: `ZoeD_N`
- `ZoeD_M12_K.pt`: `ZoeD_K`
- `ZoeD_M12_NK.pt`: `ZoeD_NK`

## USER_DOWNLOAD_ONLY List

None assigned in this audit. Entries are either safe with notice, excluded for
non-commercial/inherited-license concerns, blocked for unclear weight license,
or not a model-pack target.

## NOT_A_MODEL_PACK_TARGET List

- `NULL`: no checkpoint file.

## Recommended Public v3dfy Model-Pack Shortlist

1. `depth_anything_v2_vits.pth` / `Any_V2_S`
   - Best first pack: safe, small, already mapped, general purpose.
2. `depth_anything_metric_depth_indoor.pt` / `ZoeD_Any_N`
   - Best mapped metric indoor/base candidate, but large.
3. `depth_anything_metric_depth_outdoor.pt` / `ZoeD_Any_K`
   - Best mapped outdoor metric complement, but large and scene-specific.
4. `video_depth_anything_vits.pth` / `VDA_S`, `VDA_Stream_S`
   - Safe temporal video-depth candidate, but requires new v3dfy mapping and
     video-only flow verification.
5. `metric_video_depth_anything_vits.pth` / `VDA_Metric_S`,
   `VDA_Stream_Metric_S`
   - Safe metric video-depth candidate, but requires new v3dfy mapping and
     video-only flow verification.
6. `depth_anything_v2_metric_hypersim_vits.pth` / `Any_V2_N_S`
   - Safe small indoor metric V2 candidate if v3dfy wants a smaller metric
     indoor alternative to `ZoeD_Any_N`.

Do not include every safe model automatically. `DepthPro` is image-only in iw3;
DA3 and v1 Large are large/redundant; Distill Small needs quality validation;
safe base-size metric models may be useful later but are lower priority.

## Models Mapped By v3dfy But Not Safe To Redistribute

- `zoedepth-indoor` / `ZoeD_N` / `ZoeD_M12_N.pt`:
  `BLOCKED_UNCLEAR_LICENSE`
- `zoedepth-outdoor` / `ZoeD_K` / `ZoeD_M12_K.pt`:
  `BLOCKED_UNCLEAR_LICENSE`
- `zoedepth-indoor-outdoor` / `ZoeD_NK` / `ZoeD_M12_NK.pt`:
  `BLOCKED_UNCLEAR_LICENSE`

The other mapped entries are `SAFE_WITH_NOTICE`.

## Safe Models Not Currently Mapped By v3dfy

- `Any_L`
- `Any_V2_N_S`
- `Any_V2_N_B`
- `Any_V2_K_S`
- `Any_V2_K_B`
- `Distill_Any_S`
- `Any_V3_Mono`
- `Any_V3_Mono_01`
- `DepthPro`
- `DepthPro_S`
- `VDA_S`
- `VDA_Stream_S`
- `VDA_Metric_S`
- `VDA_Stream_Metric_S`

Do not add these as selectable mappings until the exact bundled iw3 CLI is
verified and v3dfy has product reasons to expose them.

## Required License/Notice File Layout For SAFE_WITH_NOTICE Packs

Each public model pack should include:

```text
licenses/models/<pack-id>/
  LICENSE-<upstream>.txt
  MODEL_CARD.md
  SOURCE.txt
  CITATION.txt
  NOTICE.txt
```

`SOURCE.txt` should include:

- official checkpoint URL;
- official model card URL, when applicable;
- official repository URL;
- upstream model family;
- exact checkpoint filename used by upstream;
- exact iw3 expected filename and install path;
- retrieval date;
- SHA256 checksum captured during the later approved download step;
- note if v3dfy renames the upstream file for iw3 compatibility.

Required license/source sets:

- Depth Anything v1 and metric packs:
  `LiheYoung/Depth-Anything` Apache-2.0 license, HF Space metadata or README,
  Depth Anything citation.
- Depth Anything V2 packs:
  `DepthAnything/Depth-Anything-V2` Apache-2.0 license, exact HF model card,
  Depth Anything V2 and V1 citations.
- Distill Any Depth Small:
  HF checkpoint Apache-2.0 metadata, project MIT code license, exact HF model
  card, project README, Distill Any Depth citation.
- Depth Anything 3:
  Apache-2.0 license, exact DA3MONO-LARGE HF model card, upstream README,
  DA3 citation.
- Depth Pro:
  Apple `ml-depth-pro` LICENSE, `ACKNOWLEDGEMENTS.md`, README/source metadata,
  Depth Pro citation, and a notice that Apple names/trademarks cannot be used
  to endorse v3dfy.
- Video Depth Anything Small and Metric Small:
  Apache-2.0 license, exact HF model card, upstream README, Video Depth
  Anything citation.

## Open Questions

- ZoeDepth weight redistribution remains unclear. Obtain official written
  clarification or keep ZoeDepth checkpoints out of public v3dfy assets.
- Depth Anything V2 metric Large model cards say Apache-2.0, but the official
  iw3 README marks the exact large metric iw3 checkpoints as CC-BY-NC-4.0.
  Keep them excluded until upstream clarifies the conflict.
- Distill Any Depth Base/Large have Apache metadata in the raw checkpoint repo,
  but iw3 warns about Depth Anything V2 non-commercial initial weights. Keep
  them excluded until upstream clarifies derivative-weight licensing.
- Any safe but currently unmapped model still needs bundled iw3 CLI verification
  and product testing before becoming selectable.
- Checksums must be recorded later during an explicitly approved download step.

## Next Step Recommendation

Build the first public model-pack implementation around
`depth-anything-v2-small` only, using placeholder bytes in tests until a later
approved download/checksum step. It is safe with notice, already mapped by
v3dfy, small enough for release-workflow validation, and useful as a general
optional model.

After that, prepare the notice/source layout for
`depth-anything-metric-indoor` as the mapped metric base candidate, but do not
ship the real checkpoint until checksum capture, model-pack import, engine
bundle, and conversion validation are explicitly approved.
