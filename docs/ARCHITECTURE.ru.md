# РђСЂС…РёС‚РµРєС‚СѓСЂР° Avalonia3D (RU)

## 1. РћР±С‰Р°СЏ СЃС…РµРјР°

Р РµС€РµРЅРёРµ СЃРѕСЃС‚РѕРёС‚ РёР· С‚СЂРµС… РїСЂРѕРµРєС‚РѕРІ:

- `Avalonia3D` - СЏРґСЂРѕ РґРІРёР¶РєР°.
- `Avalonia3D.Sandbox` - UI-С…РѕСЃС‚, РёРЅС‚РµСЂР°РєС‚РёРІРЅР°СЏ СЃСЂРµРґР° С‚РµСЃС‚РёСЂРѕРІР°РЅРёСЏ.
- `Avalonia3D.Tests` - РјРѕРґСѓР»СЊРЅС‹Рµ/РёРЅС‚РµРіСЂР°С†РёРѕРЅРЅС‹Рµ С‚РµСЃС‚С‹ РґРѕРјРµРЅРЅРѕР№ Р»РѕРіРёРєРё.

РџРѕС‚РѕРє РґР°РЅРЅС‹С… РІ СЂР°РЅС‚Р°Р№РјРµ:

1. UI/СЃС†РµРЅР° РёРЅРёС†РёРёСЂСѓРµС‚ Р·Р°РіСЂСѓР·РєСѓ (`SceneLoader`, `RenderThreadSceneLoadOrchestrator`).
2. РРјРїРѕСЂС‚РµСЂ (`GltfSceneImporter`) С‡РёС‚Р°РµС‚ glTF/glb, РїСЂРёРјРµРЅСЏРµС‚ РїРѕР»РёС‚РёРєСѓ РІР°Р»РёРґР°С†РёРё Рё material policy.
3. `Scene3D` РїРѕР»СѓС‡Р°РµС‚ `SceneImportResult`, РїРµСЂРµСЃС‚СЂР°РёРІР°РµС‚ `SceneGraph`, СЂРµРіРёСЃС‚СЂРёСЂСѓРµС‚ РєР»РёРїС‹ Р°РЅРёРјР°С†РёРё.
4. Р РµРЅРґРµСЂ-РєРѕРЅС‚СЂРѕР» (`SandboxModel3DControl`) РёСЃРїРѕР»РЅСЏРµС‚ render-thread queue Рё РІС‹Р·С‹РІР°РµС‚ `RenderPipeline`.
5. `RenderPipeline` РІС‹РїРѕР»РЅСЏРµС‚ РЅР°Р±РѕСЂ pass-РѕРІ (`Shadow`, `Environment`, `Forward`, post-effects).

## 2. РўРѕС‡РєРё РІС…РѕРґР°

- [Program.cs](../Program.cs) в `Avalonia3D` и [Avalonia3D.Sandbox/Program.cs](../Avalonia3D.Sandbox/Program.cs):
  - РїСЂРёРјРµРЅСЏСЋС‚ РїРѕР»РёС‚РёРєРё РёРјРїРѕСЂС‚Р°/alpha override;
  - РІС‹Р±РёСЂР°СЋС‚ desktop startup РёР»Рё Linux DRM startup;
  - РєРѕРЅС„РёРіСѓСЂРёСЂСѓСЋС‚ Avalonia app builder.

- [App.axaml.cs](../App.axaml.cs) и [Avalonia3D.Sandbox/App.axaml.cs](../Avalonia3D.Sandbox/App.axaml.cs):
  - РёРЅРёС†РёР°Р»РёР·Р°С†РёСЏ root window (`MainWindow`).

## 3. РЎР»РѕРё СЃРёСЃС‚РµРјС‹

### 3.1 Domain Scene Layer

РљР»СЋС‡РµРІРѕР№ РєР»Р°СЃСЃ: [Model/Scene3D.cs](../Model/Scene3D.cs).

РћС‚РІРµС‚СЃС‚РІРµРЅРЅРѕСЃС‚Рё:

- С…СЂР°РЅРµРЅРёРµ Р°РєС‚СѓР°Р»СЊРЅРѕРіРѕ `SceneGraph`;
- СѓРїСЂР°РІР»РµРЅРёРµ РїРѕРІРµРґРµРЅРёСЏРјРё (`ISceneBehavior`) Рё РјРѕРґСѓР»СЏРјРё (`ISceneModule`);
- СѓРїСЂР°РІР»РµРЅРёРµ Р°РЅРёРјР°С†РёСЏРјРё С‡РµСЂРµР· `AnimatorComponent`;
- РґРёСЃРїРµС‚С‡РµСЂРёР·Р°С†РёСЏ РєРѕРјР°РЅРґ (`SceneCommandBus`) РґР»СЏ runtime-РїРѕРІРµРґРµРЅРёР№;
- С…СЂР°РЅРµРЅРёРµ render mode, graphics profile Рё import report.

### 3.2 Import Layer

РљР»СЋС‡РµРІС‹Рµ С„Р°Р№Р»С‹:

- [Loaders/GltfSceneImporter.cs](../Loaders/GltfSceneImporter.cs)
- [Loaders/ModelLoader.cs](../Loaders/ModelLoader.cs)
- [Loaders/Policies/DefaultMaterialImportPolicy.cs](../Loaders/Policies/DefaultMaterialImportPolicy.cs)
- [Loaders/MaterialAlphaImportPolicy.cs](../Loaders/MaterialAlphaImportPolicy.cs)

Р’РѕР·РјРѕР¶РЅРѕСЃС‚Рё:

- strict/relaxed СЃС‚СЂР°С‚РµРіРёСЏ РІР°Р»РёРґР°С†РёРё glTF;
- fallback-СЂРµР¶РёРј РґРµРіСЂР°РґРёСЂРѕРІР°РЅРЅРѕР№ Р·Р°РіСЂСѓР·РєРё РІРјРµСЃС‚Рѕ Р°РІР°СЂРёР№РЅРѕРіРѕ РїР°РґРµРЅРёСЏ;
- РјР°С‚РµСЂРёР°Р»-РїРѕР»РёС‚РёРєР° РїРѕ alpha mode Рё СЌРІСЂРёСЃС‚РёРєРё РїРѕ РїСЂРѕР·СЂР°С‡РЅРѕСЃС‚Рё С‚РµРєСЃС‚СѓСЂ;
- precomputed material map Рё texture decode cache;
- РёР·РІР»РµС‡РµРЅРёРµ РєР°РЅР°Р»РѕРІ Р°РЅРёРјР°С†РёРё TRS/morph/material/texture-transform.

### 3.3 Render Layer

РљР»СЋС‡РµРІС‹Рµ С„Р°Р№Р»С‹:

- [Rendering/RenderPipeline.cs](../Rendering/RenderPipeline.cs)
- [Rendering/RenderPipelineFactory.cs](../Rendering/RenderPipelineFactory.cs)
- [Rendering/RenderResourceManager.cs](../Rendering/RenderResourceManager.cs)
- [Rendering/ForwardPass.cs](../Rendering/ForwardPass.cs)
- [Rendering/ShadowPass.cs](../Rendering/ShadowPass.cs)
- [Rendering/EnvironmentLightingPass.cs](../Rendering/EnvironmentLightingPass.cs)
- [Rendering/BloomPass.cs](../Rendering/BloomPass.cs)
- [Rendering/PostEffectsPass.cs](../Rendering/PostEffectsPass.cs)

РџСЂРѕС†РµСЃСЃ:

1. РЎР±РѕСЂ mesh-РѕР±СЉРµРєС‚РѕРІ РёР· РґРµСЂРµРІР°.
2. Frustum culling.
3. Р Р°Р·РґРµР»РµРЅРёРµ РЅР° opaque/transparent.
4. РЎРѕСЂС‚РёСЂРѕРІРєР° transparent back-to-front.
5. Р’С‹РїРѕР»РЅРµРЅРёРµ pass-РѕРІ РІ РїРѕСЂСЏРґРєРµ, Р·Р°РґР°РЅРЅРѕРј `RenderPipelineFactory`.

### 3.4 Shader Layer

РљР»СЋС‡РµРІС‹Рµ С„Р°Р№Р»С‹:

- [Rendering/ShaderSelectionPolicy.cs](../Rendering/ShaderSelectionPolicy.cs)
- [Rendering/RuntimePbrShaderFactory.cs](../Rendering/RuntimePbrShaderFactory.cs)
- [Shaders/PbrShaderSourceBuilder.cs](../Shaders/PbrShaderSourceBuilder.cs)
- [Shaders/ShaderIds.cs](../Shaders/ShaderIds.cs)

Р›РѕРіРёРєР°:

- РїСЂРёРѕСЂРёС‚РµС‚ explicit material shader > material shader id > СЂРµР¶РёРј СЃС†РµРЅС‹ > PBR variant;
- РїСЂРё РЅРµРѕР±С…РѕРґРёРјРѕСЃС‚Рё runtime-РєРѕРјРїРёР»СЏС†РёСЏ РІР°СЂРёР°РЅС‚Р° PBR;
- fallback chain РїСЂРё РЅРµСѓСЃРїРµС…Рµ РєРѕРјРїРёР»СЏС†РёРё (СЂРµРґСѓС†РёСЂРѕРІР°РЅРЅС‹Рµ С„РёС‡Рё/Р±Р°Р·РѕРІС‹Р№ PBR/default).

### 3.5 Interaction/Behavior Layer

РљР»СЋС‡РµРІС‹Рµ С„Р°Р№Р»С‹:

- [Interaction/CameraController/*](../Interaction/CameraController/)
- [Interaction/Behaviors/DoorBehavior.cs](../Interaction/Behaviors/DoorBehavior.cs)
- [Interaction/Behaviors/WheelRotationBehavior.cs](../Interaction/Behaviors/WheelRotationBehavior.cs)

РњРµС…Р°РЅРёРєР°:

- РєР°РјРµСЂР° РѕР±СЂР°Р±Р°С‚С‹РІР°РµС‚ РјС‹С€СЊ/РєР»Р°РІРёР°С‚СѓСЂСѓ Рё mode switching;
- behavior-РѕР±СЉРµРєС‚С‹ РїРѕР»СѓС‡Р°СЋС‚ РєРѕРјР°РЅРґС‹ (`open/close/toggle`) С‡РµСЂРµР· `SceneCommandBus`;
- С‡Р°СЃС‚СЊ behaviors С‚Р°РєР¶Рµ РѕР±РЅРѕРІР»СЏРµС‚СЃСЏ РєР°Р¶РґС‹Р№ РєР°РґСЂ (`IUpdatableBehavior`).

### 3.6 Sandbox Orchestration Layer

РљР»СЋС‡РµРІС‹Рµ С„Р°Р№Р»С‹:

- [Avalonia3D.Sandbox/Controls/SandboxModel3DControl.cs](../Avalonia3D.Sandbox/Controls/SandboxModel3DControl.cs)
- [Avalonia3D.Sandbox/Services/RenderThreadSceneLoadOrchestrator.cs](../Avalonia3D.Sandbox/Services/RenderThreadSceneLoadOrchestrator.cs)
- [Avalonia3D.Sandbox/Services/SceneLoadService.cs](../Avalonia3D.Sandbox/Services/SceneLoadService.cs)
- [Avalonia3D.Sandbox/ViewModels/MainWindowViewModel.cs](../Avalonia3D.Sandbox/ViewModels/MainWindowViewModel.cs)

РќР°Р·РЅР°С‡РµРЅРёРµ:

- РїРѕС‚РѕРєРѕР±РµР·РѕРїР°СЃРЅР°СЏ Р·Р°РіСЂСѓР·РєР° СЃС†РµРЅ СЃ РѕС‚РјРµРЅРѕР№ СѓСЃС‚Р°СЂРµРІС€РёС… Р·Р°РїСЂРѕСЃРѕРІ;
- С„РѕРЅРѕРІС‹Рµ prepare-С€Р°РіРё РїРµСЂРµРґ apply РЅР° render thread;
- РєСЌС€РёСЂРѕРІР°РЅРёРµ Рё РґРёР°РіРЅРѕСЃС‚РёРєР° СЃС†РµРЅ;
- СѓРїСЂР°РІР»РµРЅРёРµ РїСЂРѕС„РёР»СЏРјРё РіСЂР°С„РёРєРё, РѕС‚Р»Р°РґРѕС‡РЅС‹РјРё СЂРµР¶РёРјР°РјРё PBR Рё РєР»РёРїР°РјРё Р°РЅРёРјР°С†РёР№.

## 4. Р–РёР·РЅРµРЅРЅС‹Р№ С†РёРєР» СЃС†РµРЅС‹

1. Р’С‹Р±РѕСЂ СЃС†РµРЅС‹ РІ UI.
2. `SceneCatalog` СЂР°Р·СЂРµС€Р°РµС‚ id РІ `ISandboxScene`.
3. `RenderThreadSceneLoadOrchestrator.Load(...)`:
   - РѕРїС†РёРѕРЅР°Р»СЊРЅРѕ РІС‹РіСЂСѓР¶Р°РµС‚ С‚РµРєСѓС‰СѓСЋ СЃС†РµРЅСѓ;
   - РІС‹РїРѕР»РЅСЏРµС‚ background prepare;
   - РїСЂРёРјРµРЅСЏРµС‚ С‚РѕР»СЊРєРѕ РїРѕСЃР»РµРґРЅРёР№ Р°РєС‚СѓР°Р»СЊРЅС‹Р№ Р·Р°РїСЂРѕСЃ.
4. `SceneLoadService.LoadNow(...)`:
   - РїСЂРёРјРµРЅСЏРµС‚ camera policy;
   - Р·Р°РіСЂСѓР¶Р°РµС‚ СЃС†РµРЅСѓ РёР»Рё prepared payload;
   - РїСѓР±Р»РёРєСѓРµС‚ РґРёР°РіРЅРѕСЃС‚РёРєСѓ Рё СЃРѕР±С‹С‚РёРµ `SceneChanged`.
5. ViewModel РїРѕР»СѓС‡Р°РµС‚ `SceneLoaded`, РѕР±РЅРѕРІР»СЏРµС‚ UI-СЃРѕСЃС‚РѕСЏРЅРёРµ.

## 5. Р“СЂР°С„РёС‡РµСЃРєРёРµ РїСЂРѕС„РёР»Рё

[Rendering/GraphicsProfile.cs](../Rendering/GraphicsProfile.cs) РѕРїСЂРµРґРµР»СЏРµС‚:

- `Low`, `Medium`, `High`, `Ultra`, `PbrDebugNeutral`;
- shadow map size, post effects, bloom, reflections, exposure/IBL, background;
- СЃРµСЂРёР°Р»РёР·Р°С†РёСЋ/РґРµСЃРµСЂРёР°Р»РёР·Р°С†РёСЋ РїСЂРѕС„РёР»СЏ РґР»СЏ UI-СЂРµРґР°РєС‚РѕСЂР° JSON.

## 6. РџР°РјСЏС‚СЊ Рё РєРµС€Рё

РљР»СЋС‡РµРІС‹Рµ РјРµС…Р°РЅРёРєРё:

- [Memory/MemoryManager.cs](../Memory/MemoryManager.cs) - РјСЏРіРєР°СЏ РѕС‡РёСЃС‚РєР°/СЂРµР¶РёРјС‹ РїРѕРґ РєСЂСѓРїРЅС‹Рµ Р°СЃСЃРµС‚С‹.
- `ModelLoader`:
  - LRU texture decode cache;
  - material index map cache;
  - persisted texture bytes cache.
- `GltfSceneImporter`:
  - model cache СЃ TTL/size limit;
  - РѕС‡РёСЃС‚РєР° РїРѕ memory pressure policy.

## 7. Р”РёР°РіРЅРѕСЃС‚РёРєР° Рё РєР°С‡РµСЃС‚РІРѕ

- `Scene3D.LastImportReport` С…СЂР°РЅРёС‚ СЃС‚Р°С‚СѓСЃ (Success/Degraded) Рё СЃРїРёСЃРѕРє РїСЂРѕР±Р»РµРј.
- [Rendering/Diagnostics/MaterialRenderDiagnostics.cs](../Rendering/Diagnostics/MaterialRenderDiagnostics.cs) РґР»СЏ PBR-С‚СЂР°СЃСЃРёСЂРѕРІРєРё РјР°С‚РµСЂРёР°Р»РѕРІ.
- Python preflight:
  - [tools/validate_gltf_assets.py](../tools/validate_gltf_assets.py)
  - [tools/validate_pbr_snapshot.py](../tools/validate_pbr_snapshot.py)

## 8. РўРµСЃС‚РѕРІР°СЏ СЃС‚СЂР°С‚РµРіРёСЏ

`Avalonia3D.Tests` РїРѕРєСЂС‹РІР°РµС‚:

- РІС‹Р±РѕСЂ С€РµР№РґРµСЂРѕРІ Рё fallback-РїСѓС‚Рё;
- С„РѕСЂРјРёСЂРѕРІР°РЅРёРµ render pipeline;
- scene load orchestration Рё cache semantics;
- import policy/texture policy;
- behavior integration (door/wheel/command bus);
- camera controller Рё frame-state РєРѕРЅС‚СЂР°РєС‚С‹.




