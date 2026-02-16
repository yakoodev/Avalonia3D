# Pin .NET SDK With global.json

## Problem
Project targets `net8.0`, CI uses .NET 8, but local SDK is not pinned.

## Risk
- Restore/test variance across machines.
- Hard-to-reproduce local build failures.

## Files
- `global.json` (new)
- `.github/workflows/tests.yml` (consistency check)

## Acceptance Criteria
- Add `global.json` pinning to compatible .NET 8 SDK.
- Local restore/test works consistently with pinned SDK.
- CI remains green with same major SDK line.
