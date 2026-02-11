#!/usr/bin/env python3
"""Preflight validation for glTF assets used in sandbox test scenes.

Checks that animation sampler input/output accessors do not reference bufferViews
with byteStride, which is invalid for animation data in our content pipeline.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Iterable

REPO_ROOT = Path(__file__).resolve().parents[1]
ASSETS_ROOT = REPO_ROOT / "Avalonia3D.Sandbox" / "Assets" / "TestScenes"


def iter_gltf_files(root: Path) -> Iterable[Path]:
    if not root.exists():
        return []
    return sorted(root.rglob("*.gltf"))


def validate_animation_sampler_byte_stride(gltf_path: Path) -> list[str]:
    with gltf_path.open("r", encoding="utf-8") as stream:
        data = json.load(stream)

    accessors = data.get("accessors") or []
    buffer_views = data.get("bufferViews") or []
    animations = data.get("animations") or []

    errors: list[str] = []

    for animation_index, animation in enumerate(animations):
        samplers = animation.get("samplers") or []
        for sampler_index, sampler in enumerate(samplers):
            for slot in ("input", "output"):
                accessor_index = sampler.get(slot)
                if not isinstance(accessor_index, int) or accessor_index < 0 or accessor_index >= len(accessors):
                    continue

                accessor = accessors[accessor_index]
                buffer_view_index = accessor.get("bufferView")
                if not isinstance(buffer_view_index, int) or buffer_view_index < 0 or buffer_view_index >= len(buffer_views):
                    continue

                buffer_view = buffer_views[buffer_view_index]
                if "byteStride" in buffer_view:
                    stride = buffer_view.get("byteStride")
                    errors.append(
                        ""
                        f"{gltf_path.relative_to(REPO_ROOT)}: "
                        f"AnimationSampler[{animation_index}].samplers[{sampler_index}].{slot} "
                        f"accessor={accessor_index} references bufferView={buffer_view_index} "
                        f"with invalid _byteStride={stride}"
                    )

    return errors


def main() -> int:
    gltf_files = list(iter_gltf_files(ASSETS_ROOT))
    if not gltf_files:
        print(f"No glTF assets found at {ASSETS_ROOT.relative_to(REPO_ROOT)}")
        return 0

    all_errors: list[str] = []
    for gltf_file in gltf_files:
        all_errors.extend(validate_animation_sampler_byte_stride(gltf_file))

    if all_errors:
        print("Asset preflight failed:")
        for error in all_errors:
            print(f"ERROR: {error}")
        print("\nCommit blocked: fix AnimationSampler ... _byteStride violations in glTF assets.")
        return 1

    print(f"Asset preflight passed ({len(gltf_files)} files checked).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
