# Scene Node Control Guide (Car Model)

## What is already implemented in Avalonia3D

- Scene graph with per-node transform (`Position`, `Rotation`, `Scale`).
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

- `car/scene.gltf` has wheel/door nodes in hierarchy (good for per-part motion).
- It does not contain node `semanticId` extras and does not contain animation clips for doors.
- Because of this:
  - Direct node transform control works now.
  - `WheelRotationBehavior`/`DoorBehavior` are not plug-and-play without extra setup.

## 1) Rotate wheels

### Direct way (works now)

```csharp
using System.Numerics;

var wheel = scene.SceneGraph.FindNode(
    "Mitsubishi_Eclipse_Spyder_2003_F2:Wheel1A_LF_Wheel1A");

if (wheel != null)
{
    var delta = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.05f);
    wheel.Rotation = Quaternion.Normalize(delta * wheel.Rotation);
}
```

Repeat for RF/LR/RR wheel nodes.

## 2) Open doors

### Direct way (works now if pivot is correct in source model)

```csharp
using System.Numerics;

var doorL = scene.SceneGraph.FindNode(
    "Mitsubishi_Eclipse_Spyder_2003_F2:DoorLPaint_OEMPaint");

if (doorL != null)
{
    // example: +35 degrees around Y
    doorL.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 180f * 35f);
}
```

For right door you usually use opposite sign.

### Behavior-based way (requires clips)

`DoorBehavior` only plays clips like `door.main.open` / `door.main.close`.
If your model has no such clips, this behavior will not open doors.

## 3) Move whole car

Find parent vehicle node and move it:

```csharp
using System.Numerics;

var carRoot = scene.SceneGraph.FindNode("Mitsubishi_Eclipse_Spyder_2003_F2");
if (carRoot != null)
{
    carRoot.Position += new Vector3(0.2f, 0f, 0f);
}
```

If the exact root name differs, inspect top-level children of `scene.SceneGraph.Root`.

## Recommended practical approach for this model

- Use direct node transforms for wheels/doors/chassis now.
- If you need stable API bindings, add `semanticId` in glTF node `extras`.
- If you need command-style open/close for doors, add corresponding animation clips or implement runtime rotation fallback behavior.
