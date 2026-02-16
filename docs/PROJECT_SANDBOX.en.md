# Project `Avalonia3D.Sandbox` (EN)

## 1. Purpose

`Avalonia3D.Sandbox` is the interactive host used for:

- runtime render validation;
- loading and switching glTF/glb scenes;
- graphics quality tuning;
- animation and behavior command checks;
- import diagnostics visibility.

## 2. Application startup

File: `Avalonia3D.Sandbox/Program.cs`.

It applies import policies and then:

- configures console logging via `Serilog`;
- starts in desktop mode or Linux DRM mode.

## 3. UI structure

- `MainWindow.axaml`:
  - left control panel (`ModelViewportPanel`);
  - right OpenGL viewport (`SandboxModel3DControl`);
  - top-left overlay with import/profile status.

- `Controls/ModelViewportPanel.axaml`:
  - tabs: `Scenes`, `Camera`, `Graphics`, `Animation`.

## 4. Core runtime classes

### 4.1 `SandboxModel3DControl`

File: `Avalonia3D.Sandbox/Controls/SandboxModel3DControl.cs`.

Role:

- owns `SandboxRenderer3D` and `Scene3D`;
- binds camera input to viewport events;
- executes render-thread queue;
- supports active/idle render loop rates;
- triggers scene loading through `SceneLoader`.

Main properties/commands:

- `SelectedSceneId`, `IsLoading`, `LastLoadError`, `IsRendererReady`;
- `LoadSceneCommand`, `FrameSceneCommand`, `ResetCameraCommand`;
- interaction sensitivities (`RotationSensitivity`, `PanSensitivity`, `ZoomSensitivity`).

### 4.2 `MainWindowViewModel`

File: `Avalonia3D.Sandbox/ViewModels/MainWindowViewModel.cs`.

Role:

- binds UI actions to runtime commands;
- controls scene loading, graphics profiles, render modes, animation clips;
- exposes import/cache status to UI;
- contains `car2`-specific runtime animator commands.

### 4.3 `SceneCatalog`

File: `Avalonia3D.Sandbox/Scenes/SceneCatalog.cs`.

Discovers `*.gltf`/`*.glb` recursively under `Assets/TestScenes`.

### 4.4 `SceneLoader` and orchestration

Files:

- `Services/SceneLoader.cs`
- `Services/RenderThreadSceneLoadOrchestrator.cs`
- `Services/SceneLoadService.cs`

Behavior:

- each load request gets a version;
- superseded requests are canceled before apply;
- optional background preparation via `ISceneBackgroundPreparation`;
- final scene apply always runs on render thread.

## 5. Services and loading policy

Services:

- `DefaultSceneCameraPolicy` - pre/post-load camera strategy.
- `DefaultSceneDiagnosticsReporter` - import diagnostics logging.
- `CacheCoordinator` with `InMemorySceneAssetCache` and `HybridSceneImportResultCache`.
- `RenderThreadScheduler` for OpenGL-thread work scheduling.

## 6. Graphics/debug controls

Exposed through ViewModel/UI:

- quality presets (`Low/Medium/High/Ultra/PbrDebugNeutral/Custom`);
- render mode switch (`PBR/Unlit/NormalsDebug`);
- emissive texture debug mode;
- PBR debug view mode;
- profile JSON editor and apply/reset workflow;
- runtime environment-map path update.

## 7. Assets and QA

Folder: `Avalonia3D.Sandbox/Assets/TestScenes`.

Includes:

- test glTF/glb assets;
- QA checklists/registries (`PBR_QA_CHECKLIST`, `ANIMATION_QA_CHECKLIST`);
- import override config (`material-import-overrides.json`).

Scripts under `tools/` treat this folder as the source for preflight/regression checks.

## 8. Practical debug workflow

1. Launch sandbox.
2. Open a scene from `Scenes`.
3. Inspect `ImportStatusText` and degraded-import status.
4. Toggle `PBR <-> Unlit`.
5. Adjust quality profile, exposure, and reflection intensity.
6. Verify animation controls (play/pause/loop) and behavior commands.
