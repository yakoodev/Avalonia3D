# Add Policy For GltfSceneImporter Model Cache

## Problem
`GltfSceneImporter` keeps `_modelCache` without explicit eviction/invalidation lifecycle.

## Risk
- RAM growth over time.
- Stale models when source file changes at same path.

## Files
- `Loaders/GltfSceneImporter.cs`
- `Model/Scene3D.cs` (integration with global cache clear path)

## Acceptance Criteria
- Introduce cache invalidation strategy (timestamp/hash/version or explicit clear API).
- Add size and/or age limit.
- Wire global clear path to importer cache where appropriate.
