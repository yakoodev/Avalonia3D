# Add Limits For Persisted Texture Cache

## Problem
Model loader persists original texture bytes to temp storage without TTL/size cap/cleanup.

## Risk
- Unbounded `%TEMP%` growth.
- Duplicate persisted payloads and long-term disk bloat.

## Files
- `Loaders/ModelLoader.cs`

## Acceptance Criteria
- Persistence is guarded by config/flag.
- Add retention strategy (TTL and/or size budget).
- Add cleanup path and basic observability (log counters).
