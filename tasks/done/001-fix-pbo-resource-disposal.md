# Fix PBO Resource Disposal

## Problem
`PboFramePresenter` allocates Pixel Pack Buffers via `gl.GenBuffers(...)` but does not release them in `Dispose()`.

## Risk
- GPU memory leak on control/context recreation.
- Potential GL instability during long-running sessions.

## Files
- `Rendering/FramePresenter.cs`

## Acceptance Criteria
- PBO ids are deleted in `Dispose()` (and/or on GL deinit path).
- Disposal is idempotent and safe.
- No regression in frame readback behavior.
