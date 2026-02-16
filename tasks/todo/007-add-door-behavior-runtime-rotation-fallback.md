# Add DoorBehavior Runtime Rotation Fallback

## Problem
`DoorBehavior` only works when matching open/close animation clips exist.
For many vehicle assets, doors are separate nodes but no clips are provided.

## Risk
- Open/close command path is unavailable for clip-less models.
- Users must bypass behavior layer and write ad-hoc node rotation logic.

## Files
- `Interaction/Behaviors/DoorBehavior.cs`
- `Interaction/Behaviors/SceneCommand.cs` (optional payload usage)
- `Avalonia3D.Tests/BehaviorIntegrationTests.cs`

## Acceptance Criteria
- If clips are missing, behavior can optionally apply runtime node rotation (hinge mode).
- Fallback is configurable (target node key, axis, open angle).
- Existing clip-driven path keeps current behavior.
- Tests cover clip path and fallback path.
