#!/usr/bin/env python3
"""Lightweight regression check for sandbox PBR QA assets.

Validation is intentionally non pixel-perfect and texture-driven:
- verifies that baseColor textures are referenced by materials,
- compares PBR proxy brightness against Unlit proxy brightness,
- controls fraction of overexposed pixels in PBR proxy,
- prints material/semantic breakdown for easier triage.
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

OVEREXPOSED_THRESHOLD = 245


@dataclass(frozen=True)
class QaAsset:
    relative_path: str
    display_name: str
    include_in_snapshot_checks: bool
    min_mean_brightness: float
    max_mean_brightness: float
    max_near_white_ratio: float
    max_pbr_to_unlit_delta: float
    max_pbr_overexposed_ratio: float


@dataclass(frozen=True)
class ImageStats:
    mean_brightness: float
    near_white_ratio: float


@dataclass(frozen=True)
class MaterialTextureBinding:
    material_index: int
    material_name: str
    semantic: str
    texture_index: int
    image_index: int
    image_path: Path


@dataclass(frozen=True)
class MaterialAggregateStats:
    material_index: int
    material_name: str
    unlit_mean: float
    pbr_mean: float
    pbr_overexposed_ratio: float


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
                max_pbr_to_unlit_delta=float(item.get("maxPbrToUnlitBrightnessDelta", 0.22)),
                max_pbr_overexposed_ratio=float(item.get("maxPbrOverexposedRatio", item.get("maxNearWhiteRatio", 1.0))),
            )
        )

    return result


def _resolve_texture_binding(
    semantic: str,
    texture_ref: object,
    textures: list[dict],
    images: list[dict],
    gltf_dir: Path,
    material_index: int,
    material_name: str,
) -> MaterialTextureBinding | None:
    if not isinstance(texture_ref, dict):
        return None

    texture_index = texture_ref.get("index")
    if not isinstance(texture_index, int) or texture_index < 0 or texture_index >= len(textures):
        return None

    texture_entry = textures[texture_index]
    source_index = texture_entry.get("source")
    if not isinstance(source_index, int) or source_index < 0 or source_index >= len(images):
        return None

    image_entry = images[source_index]
    uri = image_entry.get("uri")
    if not isinstance(uri, str) or not uri.strip():
        return None

    return MaterialTextureBinding(
        material_index=material_index,
        material_name=material_name,
        semantic=semantic,
        texture_index=texture_index,
        image_index=source_index,
        image_path=(gltf_dir / uri).resolve(),
    )


def collect_material_bindings(gltf_data: dict, gltf_dir: Path) -> list[MaterialTextureBinding]:
    materials = gltf_data.get("materials") or []
    textures = gltf_data.get("textures") or []
    images = gltf_data.get("images") or []

    bindings: list[MaterialTextureBinding] = []

    for material_index, material in enumerate(materials):
        material_name = str(material.get("name") or f"material_{material_index}")
        pbr = material.get("pbrMetallicRoughness") or {}

        specs = [
            ("baseColor", pbr.get("baseColorTexture")),
            ("metallicRoughness", pbr.get("metallicRoughnessTexture")),
            ("normal", material.get("normalTexture")),
            ("occlusion", material.get("occlusionTexture")),
            ("emissive", material.get("emissiveTexture")),
        ]

        for semantic, texture_ref in specs:
            binding = _resolve_texture_binding(
                semantic,
                texture_ref,
                textures,
                images,
                gltf_dir,
                material_index,
                material_name,
            )
            if binding is not None:
                bindings.append(binding)

    return bindings


def iter_base_color_images(bindings: Iterable[MaterialTextureBinding]) -> Iterable[Path]:
    for item in bindings:
        if item.semantic == "baseColor":
            yield item.image_path


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
        if r >= OVEREXPOSED_THRESHOLD and g >= OVEREXPOSED_THRESHOLD and b >= OVEREXPOSED_THRESHOLD:
            near_white += 1

    return ImageStats(
        mean_brightness=brightness_sum / count,
        near_white_ratio=near_white / count,
    )


def _semantic_weight(semantic: str) -> float:
    # Conservative texture-only proxy to catch white-wash regressions.
    return {
        "baseColor": 1.00,
        "emissive": 0.35,
        "occlusion": 0.10,
        "metallicRoughness": 0.08,
        "normal": 0.02,
    }.get(semantic, 0.0)


def _aggregate_metric(values: Iterable[float], default: float = 0.0) -> float:
    vals = list(values)
    if not vals:
        return default
    return sum(vals) / len(vals)


def validate_asset(asset: QaAsset) -> list[str]:
    errors: list[str] = []
    gltf_path = (ASSETS_ROOT / asset.relative_path).resolve()

    if not gltf_path.exists():
        return [f"{asset.relative_path}: файл не найден"]

    gltf_data = json.loads(gltf_path.read_text(encoding="utf-8"))
    bindings = collect_material_bindings(gltf_data, gltf_path.parent)

    base_color_paths = sorted(set(iter_base_color_images(bindings)))
    if not base_color_paths:
        return [f"{asset.relative_path}: в материалах не найдены baseColorTexture"]

    image_stats_cache: dict[Path, ImageStats] = {}
    for binding in bindings:
        if not binding.image_path.exists():
            errors.append(f"{asset.relative_path}: отсутствует texture uri -> {binding.image_path}")
            continue
        if binding.image_path not in image_stats_cache:
            image_stats_cache[binding.image_path] = compute_stats(binding.image_path)

    if not image_stats_cache:
        errors.append(f"{asset.relative_path}: не удалось собрать статистику текстур")
        return errors

    base_color_stats = [image_stats_cache[path] for path in base_color_paths if path in image_stats_cache]
    if not base_color_stats:
        errors.append(f"{asset.relative_path}: статистика baseColor недоступна")
        return errors

    mean = _aggregate_metric(item.mean_brightness for item in base_color_stats)
    near_white = _aggregate_metric(item.near_white_ratio for item in base_color_stats)

    by_material: dict[tuple[int, str], list[MaterialTextureBinding]] = {}
    by_semantic: dict[str, list[MaterialTextureBinding]] = {}

    for binding in bindings:
        if binding.image_path not in image_stats_cache:
            continue
        by_material.setdefault((binding.material_index, binding.material_name), []).append(binding)
        by_semantic.setdefault(binding.semantic, []).append(binding)

    material_stats: list[MaterialAggregateStats] = []
    for (material_index, material_name), items in sorted(by_material.items(), key=lambda kv: kv[0][0]):
        base_color_items = [x for x in items if x.semantic == "baseColor"]
        unlit_mean = _aggregate_metric(
            image_stats_cache[x.image_path].mean_brightness for x in base_color_items
        )

        weighted_sum = 0.0
        weight_total = 0.0
        overexposed_values: list[float] = []
        for item in items:
            weight = _semantic_weight(item.semantic)
            if weight <= 0:
                continue
            st = image_stats_cache[item.image_path]
            weighted_sum += st.mean_brightness * weight
            weight_total += weight
            if item.semantic in {"baseColor", "emissive"}:
                overexposed_values.append(st.near_white_ratio)

        pbr_mean = unlit_mean if weight_total == 0 else weighted_sum / weight_total
        pbr_overexposed = _aggregate_metric(overexposed_values)

        material_stats.append(
            MaterialAggregateStats(
                material_index=material_index,
                material_name=material_name,
                unlit_mean=unlit_mean,
                pbr_mean=pbr_mean,
                pbr_overexposed_ratio=pbr_overexposed,
            )
        )

    scene_unlit_mean = _aggregate_metric(ms.unlit_mean for ms in material_stats)
    scene_pbr_mean = _aggregate_metric(ms.pbr_mean for ms in material_stats)
    scene_pbr_overexposed = _aggregate_metric(ms.pbr_overexposed_ratio for ms in material_stats)
    brightness_delta = scene_pbr_mean - scene_unlit_mean

    print(
        f"[PBR Snapshot] {asset.display_name}: textures={len(image_stats_cache)} "
        f"baseMean={mean:.4f} baseNearWhite={near_white:.4f} "
        f"unlitMean={scene_unlit_mean:.4f} pbrMean={scene_pbr_mean:.4f} "
        f"pbrOverexposed={scene_pbr_overexposed:.4f} delta={brightness_delta:.4f}"
    )

    print("  Material breakdown:")
    for row in material_stats:
        print(
            f"    - [{row.material_index}] {row.material_name}: "
            f"unlit={row.unlit_mean:.4f} pbr={row.pbr_mean:.4f} overexposed={row.pbr_overexposed_ratio:.4f}"
        )

    print("  Semantic breakdown:")
    for semantic in sorted(by_semantic):
        semantic_stats = [image_stats_cache[item.image_path] for item in by_semantic[semantic]]
        print(
            f"    - {semantic}: textures={len(semantic_stats)} "
            f"mean={_aggregate_metric(x.mean_brightness for x in semantic_stats):.4f} "
            f"nearWhite={_aggregate_metric(x.near_white_ratio for x in semantic_stats):.4f}"
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

    if brightness_delta > asset.max_pbr_to_unlit_delta:
        errors.append(
            f"{asset.relative_path}: pbr-unlit delta={brightness_delta:.4f} превышает "
            f"{asset.max_pbr_to_unlit_delta:.4f}"
        )

    if scene_pbr_overexposed > asset.max_pbr_overexposed_ratio:
        errors.append(
            f"{asset.relative_path}: pbrOverexposedRatio={scene_pbr_overexposed:.4f} превышает "
            f"{asset.max_pbr_overexposed_ratio:.4f}"
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
