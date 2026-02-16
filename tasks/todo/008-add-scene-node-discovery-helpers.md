# Add Scene Node Discovery Helpers

## Problem
Public API has exact match lookup, but there is no convenient helper for discovery by partial name/pattern/category.
Vehicle scenarios need quick discovery of wheels/doors/root node.

## Risk
- Repeated custom tree traversal in each app.
- Fragile integrations that depend on ad-hoc node name parsing.

## Files
- `Model/SceneGraph.cs`
- `Model/SceneNode.cs` (optional)
- `docs/scene-node-control-guide.md`

## Acceptance Criteria
- Add helper APIs for node enumeration and filtered search (prefix/contains/predicate).
- Provide deterministic ordering for results.
- Update docs with examples for wheel/door/root discovery.
