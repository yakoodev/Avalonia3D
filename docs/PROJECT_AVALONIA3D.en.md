# Project `Avalonia3D` (EN)

## 1. Purpose

`Avalonia3D` is the core engine project:

- scene model (`Scene3D`, `SceneGraph`, `SceneNode`);
- glTF/glb import (`GltfSceneImporter`, `ModelLoader`);
- rendering pipeline and OpenGL resource lifecycle;
- animation and runtime behavior systems;
- shader subsystem (PBR/Unlit/Debug).

## 2. Startup entry point

File: `Program.cs`.

Before UI startup it applies:

- `ImportValidationConfiguration.Configure(...)`
- `MaterialAlphaImportConfiguration.Configure(...)`
- `MaterialImportOverrideConfiguration.ConfigureFromPath(...)`

Then:

- desktop startup via `StartWithClassicDesktopLifetime`;
- optional Linux DRM startup with `-card=` and `-resolution=`.

## 3. Main dependencies

`Avalonia3D.csproj` includes:

- Avalonia UI stack (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Skia`, etc.);
- OpenGL APIs (`Silk.NET.OpenGL`, `Silk.NET.OpenGLES`);
- glTF stack (`SharpGLTF.Core`, `SharpGLTF.Runtime`, `SharpGLTF.Toolkit`);
- image decoding (`SixLabors.ImageSharp`);
- logging (`Serilog`).

## 4. Core subsystems

### 4.1 Scene and domain model

Folder: `Model/`.

Important types:

- `Scene3D` - central runtime container.
- `SceneGraph` / `SceneNode` - hierarchical scene structure.
- `MeshObject`, `MeshGroup`, `Material`, `TextureData`.
- `EnvironmentLightingSettings` - runtime IBL/environment settings.

### 4.2 glTF import

Folder: `Loaders/`.

Important types:

- `GltfSceneImporter`:
  - loads `ModelRoot`;
  - falls back from strict to relaxed validation when needed;
  - builds `SceneImportResult`.
- `ModelLoader`:
  - geometry/material/texture extraction;
  - material policy application;
  - texture decode + resize + cache.

Policies:

- `ImportValidationConfiguration` - strict/relaxed.
- `MaterialAlphaImportConfiguration` - `strict|balanced|legacy`.
- `MaterialImportOverrideConfiguration` - JSON asset/material overrides.

### 4.3 Rendering

Folder: `Rendering/`.

Important components:

- `RenderPipeline` - collects visible objects, performs culling and transparent sorting.
- `RenderPipelineFactory` - selects pass list from `GraphicsProfile`.
- `RenderResourceManager` - GPU buffers/textures and geometry cache.
- Render passes:
  - `ShadowPass`
  - `EnvironmentLightingPass`
  - `ForwardPass`
  - `BloomPass`
  - `PostEffectsPass`

### 4.4 Shader system

Folders: `Shaders/`, plus `Rendering/ShaderSelectionPolicy.cs`.

Highlights:

- static shader registry (`ShaderRegistry`);
- feature-based PBR shader ids (`ShaderIds.CreatePbrVariantId(...)`);
- runtime PBR variant generation when static variant is missing;
- fallback to base PBR/default shader when compilation fails.

### 4.5 Animation

Folder: `Animation/`.

Key elements:

- `Animator`, `AnimatorComponent`;
- `AnimationClip`, `AnimationChannel`, keyframe interpolation;
- binding classes for node/material/texture-transform targets;
- morph-driven emissive composition support.

### 4.6 Behaviors and command bus

Folder: `Interaction/Behaviors/`.

Key elements:

- `SceneCommand`, `SceneCommandBus`;
- `DoorBehavior` (including runtime rotation fallback);
- `WheelRotationBehavior`.

## 5. GraphicsProfile and quality levels

File: `Rendering/GraphicsProfile.cs`.

Profiles:

- `Low`, `Medium`, `High`, `Ultra`, `PbrDebugNeutral`.

They configure:

- MSAA;
- shadow map size;
- post-effect flags;
- bloom;
- reflections/environment map;
- PBR tuning (exposure, IBL intensities, clamps).

## 6. Memory and caching

- `Memory/MemoryManager.cs` - soft cleanup and large-asset tuning.
- `ModelLoader`:
  - texture decode LRU cache;
  - material index map cache;
  - persisted texture-bytes cache.
- `GltfSceneImporter`:
  - imported model cache with size/age trimming.

## 7. Extension points

Common extension points:

- new `ISceneModule` implementations;
- new `ISceneBehavior`/`IUpdatableBehavior` implementations;
- new render passes via `RenderPipelineFactory` extension;
- new shader variants via `ShaderRegistry` and runtime factory;
- custom material import policy implementations.
