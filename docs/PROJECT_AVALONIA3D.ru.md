# РџСЂРѕРµРєС‚ `Avalonia3D` (RU)

## 1. РќР°Р·РЅР°С‡РµРЅРёРµ

`Avalonia3D` - СЌС‚Рѕ СЏРґСЂРѕ 3D-РґРІРёР¶РєР°:

- РјРѕРґРµР»СЊ СЃС†РµРЅС‹ (`Scene3D`, `SceneGraph`, `SceneNode`);
- РёРјРїРѕСЂС‚ glTF/glb (`GltfSceneImporter`, `ModelLoader`);
- СЂРµРЅРґРµСЂ-РїР°Р№РїР»Р°Р№РЅ Рё OpenGL-СЂРµСЃСѓСЂСЃС‹;
- СЃРёСЃС‚РµРјС‹ Р°РЅРёРјР°С†РёРё Рё runtime-РїРѕРІРµРґРµРЅРёСЏ;
- С€РµР№РґРµСЂРЅР°СЏ РїРѕРґСЃРёСЃС‚РµРјР° PBR/Unlit/Debug.

## 2. РўРѕС‡РєР° Р·Р°РїСѓСЃРєР°

Р¤Р°Р№Р»: [Program.cs](../Program.cs).

РџРµСЂРµРґ СЃС‚Р°СЂС‚РѕРј UI:

- `ImportValidationConfiguration.Configure(...)`
- `MaterialAlphaImportConfiguration.Configure(...)`
- `MaterialImportOverrideConfiguration.ConfigureFromPath(...)`

Р”Р°Р»РµРµ:

- desktop startup С‡РµСЂРµР· `StartWithClassicDesktopLifetime`;
- РЅР° Linux РІРѕР·РјРѕР¶РµРЅ DRM startup СЃ `-card=` Рё `-resolution=`.

## 3. РљР»СЋС‡РµРІС‹Рµ РїР°РєРµС‚С‹

Р’ [Avalonia3D.csproj](../Avalonia3D.csproj) РїРѕРґРєР»СЋС‡РµРЅС‹:

- Avalonia UI (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Skia`, Рё С‚.Рґ.);
- OpenGL API (`Silk.NET.OpenGL`, `Silk.NET.OpenGLES`);
- glTF СЃС‚РµРє (`SharpGLTF.Core`, `SharpGLTF.Runtime`, `SharpGLTF.Toolkit`);
- image decode (`SixLabors.ImageSharp`);
- Р»РѕРіРёСЂРѕРІР°РЅРёРµ (`Serilog`).

## 4. РћСЃРЅРѕРІРЅС‹Рµ РїРѕРґСЃРёСЃС‚РµРјС‹

### 4.1 Scene Рё domain model

РџР°РїРєР°: [Model/](../Model/).

РљР»СЋС‡РµРІС‹Рµ С‚РёРїС‹:

- `Scene3D` - С†РµРЅС‚СЂР°Р»СЊРЅС‹Р№ runtime-РєРѕРЅС‚РµР№РЅРµСЂ.
- ``SceneGraph` / `SceneNode`` - РёРµСЂР°СЂС…РёСЏ СѓР·Р»РѕРІ.
- `MeshObject`, `MeshGroup`, `Material`, `TextureData`.
- `EnvironmentLightingSettings` - runtime-РїР°СЂР°РјРµС‚СЂС‹ IBL/РѕРєСЂСѓР¶РµРЅРёСЏ.

### 4.2 РРјРїРѕСЂС‚ glTF

РџР°РїРєР°: [Loaders/](../Loaders/).

РљР»СЋС‡РµРІС‹Рµ С‚РёРїС‹:

- `GltfSceneImporter`:
  - С‡С‚РµРЅРёРµ `ModelRoot`;
  - fallback РёР· strict РІ relaxed validation;
  - РїРѕСЃС‚СЂРѕРµРЅРёРµ `SceneImportResult`.
- `ModelLoader`:
  - СЂР°Р·Р±РѕСЂ РіРµРѕРјРµС‚СЂРёРё/РјР°С‚РµСЂРёР°Р»РѕРІ/С‚РµРєСЃС‚СѓСЂ;
  - material policy;
  - texture decode + resize + cache.

РџРѕР»РёС‚РёРєРё:

- `ImportValidationConfiguration` - strict/relaxed.
- `MaterialAlphaImportConfiguration` - `strict|balanced|legacy`.
- `MaterialImportOverrideConfiguration` - asset/material overrides РёР· JSON.

### 4.3 Р РµРЅРґРµСЂ

РџР°РїРєР°: [Rendering/](../Rendering/).

РљР»СЋС‡РµРІС‹Рµ РєРѕРјРїРѕРЅРµРЅС‚С‹:

- `RenderPipeline` - СЃР±РѕСЂ РІРёРґРёРјС‹С… РѕР±СЉРµРєС‚РѕРІ, culling, СЃРѕСЂС‚РёСЂРѕРІРєР° РїСЂРѕР·СЂР°С‡РЅС‹С….
- `RenderPipelineFactory` - СЃРѕСЃС‚Р°РІ pass-РѕРІ РїРѕ `GraphicsProfile`.
- `RenderResourceManager` - GPU-Р±СѓС„РµСЂС‹/С‚РµРєСЃС‚СѓСЂС‹ Рё РєРµС€ РіРµРѕРјРµС‚СЂРёРё.
- Pass-С‹:
  - `ShadowPass`
  - `EnvironmentLightingPass`
  - `ForwardPass`
  - `BloomPass`
  - `PostEffectsPass`

### 4.4 РЁРµР№РґРµСЂС‹

РџР°РїРєРё: [Shaders/](../Shaders/), [Rendering/ShaderSelectionPolicy.cs](../Rendering/ShaderSelectionPolicy.cs).

РћСЃРѕР±РµРЅРЅРѕСЃС‚Рё:

- СЃС‚Р°С‚РёС‡РµСЃРєРёР№ registry (`ShaderRegistry`);
- feature-based PBR shader ids (`ShaderIds.CreatePbrVariantId(...)`);
- runtime РіРµРЅРµСЂР°С†РёСЏ PBR-РІР°СЂРёР°РЅС‚РѕРІ РїСЂРё РЅРµРґРѕСЃС‚Р°СЋС‰РµРј СЃС‚Р°С‚РёС‡РµСЃРєРѕРј С€РµР№РґРµСЂРµ;
- fallback Рє Р±Р°Р·РѕРІРѕРјСѓ PBR/default.

### 4.5 РђРЅРёРјР°С†РёСЏ

РџР°РїРєР°: [Animation/](../Animation/).

РљР»СЋС‡РµРІС‹Рµ СЌР»РµРјРµРЅС‚С‹:

- `Animator`, `AnimatorComponent`;
- `AnimationClip`, `AnimationChannel`, keyframe/interpolation;
- binding-РєР»Р°СЃСЃС‹ РґР»СЏ node/material/texture transform С†РµР»РµР№;
- РїРѕРґРґРµСЂР¶РєР° morph-driven emission Р»РѕРіРёРєРё.

### 4.6 РџРѕРІРµРґРµРЅРёСЏ Рё РєРѕРјР°РЅРґС‹

РџР°РїРєР°: [Interaction/Behaviors/](../Interaction/Behaviors/).

РљР»СЋС‡РµРІС‹Рµ СЌР»РµРјРµРЅС‚С‹:

- `SceneCommand`, `SceneCommandBus`;
- `DoorBehavior` (РІРєР»СЋС‡Р°СЏ runtime rotation fallback);
- `WheelRotationBehavior`.

## 5. GraphicsProfile Рё РєР°С‡РµСЃС‚РІРѕ

Р¤Р°Р№Р»: [Rendering/GraphicsProfile.cs](../Rendering/GraphicsProfile.cs).

РџСЂРѕС„РёР»Рё:

- `Low`, `Medium`, `High`, `Ultra`, `PbrDebugNeutral`.

РџРѕРєСЂС‹РІР°СЋС‚:

- MSAA;
- shadow map size;
- post effects flags;
- bloom РЅР°СЃС‚СЂРѕР№РєРё;
- reflections and environment map;
- PBR tuning (exposure, IBL intensity, clamps).

## 6. РџР°РјСЏС‚СЊ Рё РєРµС€РёСЂРѕРІР°РЅРёРµ

- [Memory/MemoryManager.cs](../Memory/MemoryManager.cs) - soft cleanup, СЂРµР¶РёРјС‹ РїРѕРґ РєСЂСѓРїРЅС‹Рµ Р°СЃСЃРµС‚С‹.
- `ModelLoader`:
  - texture decode LRU cache;
  - material index map cache;
  - persisted texture cache.
- `GltfSceneImporter`:
  - cache РёРјРїРѕСЂС‚РёСЂРѕРІР°РЅРЅС‹С… РјРѕРґРµР»РµР№ СЃ trim РїРѕ СЂР°Р·РјРµСЂСѓ/РІРѕР·СЂР°СЃС‚Сѓ.

## 7. Р Р°СЃС€РёСЂРµРЅРёРµ Рё РёРЅС‚РµРіСЂР°С†РёСЏ

РўРёРїРёС‡РЅС‹Рµ extension points:

- РЅРѕРІС‹Рµ `ISceneModule`;
- РЅРѕРІС‹Рµ ``ISceneBehavior`/`IUpdatableBehavior``;
- РЅРѕРІС‹Рµ render passes С‡РµСЂРµР· СЂР°СЃС€РёСЂРµРЅРёРµ `RenderPipelineFactory`;
- РЅРѕРІС‹Рµ shader-РІР°СЂРёР°РЅС‚С‹ С‡РµСЂРµР· `ShaderRegistry` Рё runtime factory;
- РєР°СЃС‚РѕРјРЅС‹Р№ material import policy.



