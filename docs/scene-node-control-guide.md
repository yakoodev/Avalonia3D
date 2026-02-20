# Scene Node Control Guide (Car Model)

## What is already implemented in Avalonia3D

- Scene graph with per-node transform (`Position`, `Rotation`, `Scale`).
- Deterministic node discovery API (`EnumerateNodes`, `FindNodes*`) with pre-order DFS traversal.
- Node lookup by key (`semanticId`, `stableId`, path, name).
- Runtime API for direct transform control:
  - `AnimatorComponent.SetNodePosition(...)`
  - `AnimatorComponent.SetNodeRotation(...)`
  - `AnimatorComponent.SetNodeScale(...)`
- glTF import keeps node hierarchy and local transforms.
- Built-in behaviors:
  - `WheelRotationBehavior` (continuous spin by command).
  - `DoorBehavior` (open/close via animation clips).

## Important for your prepared `car` model

- [car/scene.gltf](../Avalonia3D.Sandbox/Assets/TestScenes/car/scene.gltf) has wheel/door nodes in hierarchy (good for per-part motion).
- It does not contain node `semanticId` extras and does not contain animation clips for doors.
- Because of this:
  - Direct node transform control works now.
  - `WheelRotationBehavior`/`DoorBehavior` are not plug-and-play without extra setup.

## 1) Rotate wheels

### Direct way (works now)

```csharp
using System.Numerics;

var wheelNodes = scene.SceneGraph
    .FindNodesByNameContains("wheel", StringComparison.OrdinalIgnoreCase)
    .Where(node => node.Name?.Contains("LF", StringComparison.OrdinalIgnoreCase) == true
                || node.Name?.Contains("RF", StringComparison.OrdinalIgnoreCase) == true
                || node.Name?.Contains("LR", StringComparison.OrdinalIgnoreCase) == true
                || node.Name?.Contains("RR", StringComparison.OrdinalIgnoreCase) == true);

foreach (var wheel in wheelNodes)
{
    var delta = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.05f);
    wheel.Rotation = Quaternion.Normalize(delta * wheel.Rotation);
}
```

## 2) Open doors

### Direct way (works now if pivot is correct in source model)

```csharp
using System.Numerics;

var leftDoor = scene.SceneGraph
    .FindNodesByNameContains("door", StringComparison.OrdinalIgnoreCase)
    .FirstOrDefault(node => node.Name?.Contains("L", StringComparison.OrdinalIgnoreCase) == true);

if (leftDoor != null)
{
    // example: +35 degrees around Y
    leftDoor.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 180f * 35f);
}
```

For right door you usually use opposite sign.

### Behavior-based way (requires clips)

`DoorBehavior` only plays clips like `door.main.open` / `door.main.close`.
If your model has no such clips, this behavior will not open doors.

## 3) Move whole car

Find candidate root node dynamically and move it:

```csharp
using System.Numerics;

var carRoot = scene.SceneGraph
    .EnumerateNodes()
    .FirstOrDefault(node => node.Parent == scene.SceneGraph.Root);

if (carRoot != null)
{
    carRoot.Position += new Vector3(0.2f, 0f, 0f);
}
```

If your scene has multiple top-level roots, prefer explicit filters over name hardcoding:

```csharp
var vehicleRoot = scene.SceneGraph
    .FindNodes(node => node.Parent == scene.SceneGraph.Root
                    && node.Children.Any(child => child.Name?.Contains("wheel", StringComparison.OrdinalIgnoreCase) == true))
    .FirstOrDefault();
```

## Recommended practical approach for this model

- Use direct node transforms for wheels/doors/chassis now.
- Keep all node discovery logic in one place (predicates over `FindNodes*`), so later model changes are localized.
- If you need stable API bindings, add `semanticId` in glTF node `extras`.
- If you need command-style open/close for doors, add corresponding animation clips or implement runtime rotation fallback behavior.
