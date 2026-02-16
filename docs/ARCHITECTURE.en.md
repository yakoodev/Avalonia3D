# Avalonia3D Architecture (EN)

## 1. High-level layout

The solution has three projects:

- `Avalonia3D` - core engine.
- `Avalonia3D.Sandbox` - UI host and interactive testing app.
- `Avalonia3D.Tests` - automated domain/integration tests.

Runtime data flow:

1. UI triggers a scene load (`SceneLoader`, `RenderThreadSceneLoadOrchestrator`).
2. Import layer (`GltfSceneImporter`) reads glTF/glb and applies validation/material policies.
3. `Scene3D` receives `SceneImportResult`, rebuilds `SceneGraph`, and registers animation clips.
4. Render control (`SandboxModel3DControl`) executes render-thread work and runs `RenderPipeline`.
5. `RenderPipeline` executes passes (`Shadow`, `Environment`, `Forward`, post effects).

## 2. Entry points

- `Program.cs` in `Avalonia3D` and `Avalonia3D.Sandbox`:
  - configures import/material policies;
  - selects desktop startup or Linux DRM startup;
  - builds Avalonia app.

- `App.axaml.cs`:
  - creates the main window (`MainWindow`).

## 3. System layers

### 3.1 Domain scene layer

Main class: `Model/Scene3D.cs`.

Responsibilities:

- owns current `SceneGraph`;
- module (`ISceneModule`) and behavior (`ISceneBehavior`) lifecycle;
- animation control via `AnimatorComponent`;
- command dispatch via `SceneCommandBus`;
- render mode, graphics profile, and import report state.

### 3.2 Import layer

Key files:

- `Loaders/GltfSceneImporter.cs`
- `Loaders/ModelLoader.cs`
- `Loaders/Policies/DefaultMaterialImportPolicy.cs`
- `Loaders/MaterialAlphaImportPolicy.cs`

Capabilities:

- strict/relaxed glTF validation strategy;
- degraded-load fallback instead of hard failure;
- alpha-mode material policy with texture transparency heuristics;
- precomputed material mapping and texture decode caching;
- animation channel extraction for TRS/morph/material/texture-transform.

### 3.3 Render layer

Key files:

- `Rendering/RenderPipeline.cs`
- `Rendering/RenderPipelineFactory.cs`
- `Rendering/RenderResourceManager.cs`
- `Rendering/ForwardPass.cs`
- `Rendering/ShadowPass.cs`
- `Rendering/EnvironmentLightingPass.cs`
- `Rendering/BloomPass.cs`
- `Rendering/PostEffectsPass.cs`

Pipeline steps:

1. Collect mesh objects from scene tree.
2. Frustum culling.
3. Split into opaque/transparent buckets.
4. Sort transparent objects back-to-front.
5. Execute passes produced by `RenderPipelineFactory`.

### 3.4 Shader layer

Key files:

- `Rendering/ShaderSelectionPolicy.cs`
- `Rendering/RuntimePbrShaderFactory.cs`
- `Shaders/PbrShaderSourceBuilder.cs`
- `Shaders/ShaderIds.cs`

Selection logic:

- priority: explicit material shader > material shader id > scene mode shader > PBR feature variant;
- runtime PBR variant generation when needed;
- fallback chain for compile failures (reduced features/base PBR/default shader).

### 3.5 Interaction and behavior layer

Key files:

- `Interaction/CameraController/*`
- `Interaction/Behaviors/DoorBehavior.cs`
- `Interaction/Behaviors/WheelRotationBehavior.cs`

Behavior:

- camera input handling and control-mode switching;
- command-driven behaviors (`open/close/toggle`) through `SceneCommandBus`;
- frame-updated behaviors through `IUpdatableBehavior`.

### 3.6 Sandbox orchestration layer

Key files:

- `Avalonia3D.Sandbox/Controls/SandboxModel3DControl.cs`
- `Avalonia3D.Sandbox/Services/RenderThreadSceneLoadOrchestrator.cs`
- `Avalonia3D.Sandbox/Services/SceneLoadService.cs`
- `Avalonia3D.Sandbox/ViewModels/MainWindowViewModel.cs`

Role:

- thread-safe scene loading with superseded-request cancellation;
- background preparation before render-thread apply;
- scene cache/diagnostics;
- runtime graphics tuning, PBR debug modes, and animation clip controls.

## 4. Scene lifecycle

1. User selects a scene in UI.
2. `SceneCatalog` resolves scene id into `ISandboxScene`.
3. `RenderThreadSceneLoadOrchestrator.Load(...)`:
   - optionally unloads current scene;
   - runs optional background prepare;
   - applies only latest request version.
4. `SceneLoadService.LoadNow(...)`:
   - applies camera policy;
   - loads scene or prepared payload;
   - reports diagnostics and emits `SceneChanged`.
5. ViewModel receives `SceneLoaded` and updates UI state.

## 5. Graphics profiles

`Rendering/GraphicsProfile.cs` defines:

- `Low`, `Medium`, `High`, `Ultra`, `PbrDebugNeutral`;
- shadow map size, post effects, bloom, reflections, exposure/IBL, background;
- JSON serialization/deserialization for profile editing in UI.

## 6. Memory and caches

Core mechanisms:

- `Memory/MemoryManager.cs` for soft cleanup and large-model tuning.
- `ModelLoader`:
  - LRU texture decode cache;
  - material index map cache;
  - persisted texture-bytes cache.
- `GltfSceneImporter`:
  - model cache with TTL/size limits;
  - memory-pressure driven trimming.

## 7. Diagnostics and quality gates

- `Scene3D.LastImportReport` captures Success/Degraded status and issues.
- `Rendering/Diagnostics/MaterialRenderDiagnostics.cs` provides material render snapshots.
- Python quality/preflight scripts:
  - `tools/validate_gltf_assets.py`
  - `tools/validate_pbr_snapshot.py`

## 8. Test strategy

`Avalonia3D.Tests` validates:

- shader selection and fallback behavior;
- render pipeline construction rules;
- scene load orchestration and cache semantics;
- import and texture policy behavior;
- behavior integration (door/wheel/command bus);
- camera controller and render frame-state contracts.
