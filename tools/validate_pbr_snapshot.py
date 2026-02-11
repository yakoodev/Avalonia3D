#!/usr/bin/env python3
"""Lightweight snapshot regression check for sandbox PBR QA assets.

Non pixel-perfect validation:
- verifies that baseColor textures are referenced by materials,
- computes mean brightness and near-white ratio over referenced baseColor images,
- validates aggregated metrics against per-asset thresholds.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

try:
    from PIL import Image
except ModuleNotFoundError as exc:  # pragma: no cover
    raise SystemExit(
        "Pillow is required. Install with: pip install pillow"
    ) from exc

REPO_ROOT = Path(__file__).resolve().parents[1]
ASSETS_ROOT = REPO_ROOT / "Avalonia3D.Sandbox" / "Assets" / "TestScenes"
REGISTRY_PATH = ASSETS_ROOT / "PBR_QA_ASSETS.json"


@dataclass(frozen=True)
class QaAsset:
    relative_path: str
    display_name: str
    include_in_snapshot_checks: bool
    min_mean_brightness: float
    max_mean_brightness: float
    max_near_white_ratio: float


@dataclass(frozen=True)
class ImageStats:
    mean_brightness: float
    near_white_ratio: float


def load_registry(path: Path) -> list[QaAsset]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    result: list[QaAsset] = []

    for item in payload.get("assets", []):
        result.append(
            QaAsset(
                relative_path=item["relativePath"],
                display_name=item.get("displayName", item["relativePath"]),
                include_in_snapshot_checks=bool(item.get("includeInSnapshotChecks", True)),
                min_mean_brightness=float(item.get("minMeanBrightness", 0.0)),
                max_mean_brightness=float(item.get("maxMeanBrightness", 1.0)),
                max_near_white_ratio=float(item.get("maxNearWhiteRatio", 1.0)),
            )
        )

    return result


def iter_base_color_images(gltf_data: dict, gltf_dir: Path) -> Iterable[Path]:
    materials = gltf_data.get("materials") or []
    textures = gltf_data.get("textures") or []
    images = gltf_data.get("images") or []

    for material in materials:
        pbr = material.get("pbrMetallicRoughness") or {}
        base_color_texture = pbr.get("baseColorTexture")
        if not isinstance(base_color_texture, dict):
            continue

        texture_index = base_color_texture.get("index")
        if not isinstance(texture_index, int) or texture_index < 0 or texture_index >= len(textures):
            continue

        texture_entry = textures[texture_index]
        source_index = texture_entry.get("source")
        if not isinstance(source_index, int) or source_index < 0 or source_index >= len(images):
            continue

        image_entry = images[source_index]
        uri = image_entry.get("uri")
        if not isinstance(uri, str) or not uri.strip():
            continue

        yield (gltf_dir / uri).resolve()


def compute_stats(path: Path) -> ImageStats:
    with Image.open(path) as image:
        rgb = image.convert("RGB")
        raw = rgb.tobytes()

    count = len(raw) // 3
    if count == 0:
        return ImageStats(0.0, 0.0)

    brightness_sum = 0.0
    near_white = 0

    for index in range(0, len(raw), 3):
        r = raw[index]
        g = raw[index + 1]
        b = raw[index + 2]
        brightness = (r + g + b) / (3.0 * 255.0)
        brightness_sum += brightness
        if r >= 245 and g >= 245 and b >= 245:
            near_white += 1

    return ImageStats(
        mean_brightness=brightness_sum / count,
        near_white_ratio=near_white / count,
    )


def validate_asset(asset: QaAsset) -> list[str]:
    errors: list[str] = []
    gltf_path = (ASSETS_ROOT / asset.relative_path).resolve()

    if not gltf_path.exists():
        return [f"{asset.relative_path}: файл не найден"]

    gltf_data = json.loads(gltf_path.read_text(encoding="utf-8"))
    image_paths = sorted(set(iter_base_color_images(gltf_data, gltf_path.parent)))

    if not image_paths:
        return [f"{asset.relative_path}: в материалах не найдены baseColorTexture"]

    stats = [compute_stats(path) for path in image_paths if path.exists()]
    missing = [path for path in image_paths if not path.exists()]
    for miss in missing:
        errors.append(f"{asset.relative_path}: отсутствует texture uri -> {miss}")

    if not stats:
        errors.append(f"{asset.relative_path}: не удалось собрать статистику baseColor текстур")
        return errors

    mean = sum(item.mean_brightness for item in stats) / len(stats)
    near_white = sum(item.near_white_ratio for item in stats) / len(stats)

    print(
        f"[PBR Snapshot] {asset.display_name}: textures={len(stats)} "
        f"meanBrightness={mean:.4f} nearWhiteRatio={near_white:.4f}"
    )

    if mean < asset.min_mean_brightness or mean > asset.max_mean_brightness:
        errors.append(
            f"{asset.relative_path}: meanBrightness={mean:.4f} вне диапазона "
            f"[{asset.min_mean_brightness:.4f}, {asset.max_mean_brightness:.4f}]"
        )

    if near_white > asset.max_near_white_ratio:
        errors.append(
            f"{asset.relative_path}: nearWhiteRatio={near_white:.4f} превышает "
            f"{asset.max_near_white_ratio:.4f}"
        )

    return errors


def main() -> int:
    if not REGISTRY_PATH.exists():
        print(f"Registry not found: {REGISTRY_PATH.relative_to(REPO_ROOT)}")
        return 1

    assets = [a for a in load_registry(REGISTRY_PATH) if a.include_in_snapshot_checks]
    if not assets:
        print("No QA assets enabled for snapshot checks.")
        return 0

    all_errors: list[str] = []
    for asset in assets:
        all_errors.extend(validate_asset(asset))

    if all_errors:
        print("\nPBR snapshot check failed:")
        for error in all_errors:
            print(f"ERROR: {error}")
        return 1

    print("\nPBR snapshot check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
