import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter


def parse_args():
    parser = argparse.ArgumentParser(
        description="Generate depth-based 2.5D parallax frames for v3dfy.")
    parser.add_argument("--source", required=True)
    parser.add_argument("--depth", required=True)
    parser.add_argument("--frames-dir", required=True)
    parser.add_argument("--frame-count", required=True, type=int)
    parser.add_argument("--fps", required=True, type=int)
    parser.add_argument("--intensity", required=True)
    parser.add_argument("--direction", required=True)
    parser.add_argument("--zoom", required=True)
    parser.add_argument("--smoothing", required=True)
    parser.add_argument("--layer-behavior", required=True)
    return parser.parse_args()


def load_source(path):
    if not Path(path).is_file():
        raise FileNotFoundError(f"Source image does not exist: {path}")

    with Image.open(path) as image:
        return image.convert("RGB")


def normalize_depth_array(depth):
    depth = np.asarray(depth, dtype=np.float32)
    if depth.ndim == 3:
        depth = depth[..., 0]

    depth -= float(depth.min())
    maximum = float(depth.max())
    if maximum > 0:
        depth /= maximum

    return np.clip(depth, 0.0, 1.0)


def smooth_depth_array(depth, smoothing):
    smoothing_radius = {
        "enabled": 1.2,
        "balanced": 1.8,
        "strong": 2.6,
    }.get(smoothing.strip().lower(), 1.2)
    if smoothing_radius <= 0:
        return depth

    # Pillow cannot blur 16-bit/I-mode depth images reliably. Blur a
    # normalized 8-bit L image, then convert back to float32 depth.
    depth_l = Image.fromarray(np.clip(depth * 255.0, 0, 255).astype(np.uint8), mode="L")
    depth_l = depth_l.filter(ImageFilter.GaussianBlur(radius=smoothing_radius))
    return np.asarray(depth_l, dtype=np.float32) / 255.0


def load_depth(path, size, smoothing, layer_behavior):
    if not Path(path).is_file():
        raise FileNotFoundError(f"Depth image does not exist: {path}")

    with Image.open(path) as image:
        depth_image = image.copy()
        if depth_image.size != size:
            depth_image = depth_image.resize(size, Image.Resampling.BICUBIC)

        depth = normalize_depth_array(depth_image)
        depth = smooth_depth_array(depth, smoothing)

        behavior = layer_behavior.strip().lower()
        if "depth slices" in behavior:
            depth = np.round(depth * 5.0) / 5.0
        elif "foreground" in behavior:
            depth = depth * depth * (3.0 - 2.0 * depth)

        return np.clip(depth, 0.0, 1.0)


def map_intensity(value, width, height):
    base = min(width, height)
    normalized = value.strip().lower()
    if normalized == "low":
        return max(1.5, base * 0.02)
    if normalized == "high":
        return max(3.0, base * 0.055)
    return max(2.0, base * 0.035)


def map_zoom(value):
    normalized = value.strip().lower()
    if normalized == "subtle":
        return 0.02
    if normalized == "strong":
        return 0.055
    return 0.035


def motion_for_frame(index, frame_count, direction, amplitude):
    phase = math.sin((2.0 * math.pi * index) / max(frame_count, 1))
    orbit = math.cos((2.0 * math.pi * index) / max(frame_count, 1))
    normalized = direction.strip().lower()
    if normalized == "right to left":
        return -phase * amplitude, 0.0, phase
    if normalized == "push in":
        return 0.0, 0.0, abs(phase)
    if normalized == "pull back":
        return 0.0, 0.0, -abs(phase)
    if normalized == "orbit":
        return phase * amplitude, orbit * amplitude * 0.45, phase
    return phase * amplitude, 0.0, phase


def render_frame(source, depth, index, frame_count, intensity, direction, zoom_amount):
    height, width, _ = source.shape
    grid_y, grid_x = np.indices((height, width), dtype=np.float32)
    offset_x, offset_y, zoom_phase = motion_for_frame(index, frame_count, direction, intensity)
    centered_depth = depth - 0.5

    zoom = 1.0 + zoom_amount * zoom_phase
    center_x = (width - 1) * 0.5
    center_y = (height - 1) * 0.5
    sample_x = center_x + (grid_x - center_x) / max(zoom, 0.8)
    sample_y = center_y + (grid_y - center_y) / max(zoom, 0.8)
    sample_x -= offset_x * centered_depth
    sample_y -= offset_y * centered_depth

    sample_x = np.clip(np.rint(sample_x).astype(np.int32), 0, width - 1)
    sample_y = np.clip(np.rint(sample_y).astype(np.int32), 0, height - 1)
    return source[sample_y, sample_x]


def main():
    args = parse_args()
    if args.frame_count <= 0:
        raise ValueError("--frame-count must be greater than zero.")

    source_path = Path(args.source)
    depth_path = Path(args.depth)
    frames_dir = Path(args.frames_dir)
    frames_dir.mkdir(parents=True, exist_ok=True)

    source_image = load_source(source_path)
    source = np.asarray(source_image, dtype=np.uint8)
    depth = load_depth(depth_path, source_image.size, args.smoothing, args.layer_behavior)
    intensity = map_intensity(args.intensity, source_image.width, source_image.height)
    zoom_amount = map_zoom(args.zoom)
    frame_count = max(1, args.frame_count)

    for index in range(frame_count):
        frame = render_frame(
            source,
            depth,
            index,
            frame_count,
            intensity,
            args.direction,
            zoom_amount)
        Image.fromarray(frame, mode="RGB").save(frames_dir / f"frame_{index:05d}.png")

    print(f"Generated {frame_count} parallax frames in {frames_dir}")


if __name__ == "__main__":
    main()
