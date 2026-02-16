# Reduce Idle Render Loop Load

## Problem
`Model3DControl` continuously requests next frame in `OnOpenGlRender()`.

## Risk
- Unnecessary CPU/GPU usage when scene is idle.
- Higher battery/power usage on desktop/mobile devices.

## Files
- `Controls/Model3DControl.cs`
- `Rendering/*` (if frame scheduler is introduced)

## Acceptance Criteria
- Add render scheduling mode (on-change or capped FPS).
- Keep interaction smooth during camera input/animation.
- Verify no regression in frame delivery and visual updates.
