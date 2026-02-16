# Extend Wheel Behavior Node Targeting

## Problem
`WheelRotationBehavior` currently resolves target node only by `semanticId` and always rotates around `Vector3.UnitX`.
For imported vehicle models without semantic ids, this is not convenient.

## Risk
- Hard to reuse behavior on real assets where nodes have names but no semantic metadata.
- Extra integration code in app layer for each model.

## Files
- `Interaction/Behaviors/WheelRotationBehavior.cs`
- `Model/SceneGraph.cs` (if helper lookup extension is needed)

## Acceptance Criteria
- Behavior can target node by configurable key mode (`semanticId`/`stableId`/`name`/`path`).
- Rotation axis is configurable (`X`/`Y`/`Z` or `Vector3`).
- Existing behavior contract remains backward compatible.
