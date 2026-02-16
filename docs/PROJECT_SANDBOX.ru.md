# РџСЂРѕРµРєС‚ `Avalonia3D.Sandbox` (RU)

## 1. РќР°Р·РЅР°С‡РµРЅРёРµ

`Avalonia3D.Sandbox` - СЂР°Р±РѕС‡Р°СЏ РѕР±РѕР»РѕС‡РєР° РґР»СЏ:

- РёРЅС‚РµСЂР°РєС‚РёРІРЅРѕР№ РїСЂРѕРІРµСЂРєРё СЂРµРЅРґРµСЂР°;
- РїСЂРѕСЃРјРѕС‚СЂР° Рё РїРµСЂРµРєР»СЋС‡РµРЅРёСЏ glTF/glb СЃС†РµРЅ;
- РЅР°СЃС‚СЂРѕР№РєРё РєР°С‡РµСЃС‚РІР° РіСЂР°С„РёРєРё РІ СЂР°РЅС‚Р°Р№РјРµ;
- РїСЂРѕРІРµСЂРєРё Р°РЅРёРјР°С†РёР№ Рё behavior-РєРѕРјР°РЅРґ;
- РґРёР°РіРЅРѕСЃС‚РёРєРё РїСЂРѕР±Р»РµРј РёРјРїРѕСЂС‚Р°.

## 2. РЎС‚Р°СЂС‚ РїСЂРёР»РѕР¶РµРЅРёСЏ

Р¤Р°Р№Р»: [Avalonia3D.Sandbox/Program.cs](../Avalonia3D.Sandbox/Program.cs).

Р”РµР»Р°РµС‚ С‚Рѕ Р¶Рµ Р±Р°Р·РѕРІРѕРµ РєРѕРЅС„РёРіСѓСЂРёСЂРѕРІР°РЅРёРµ РёРјРїРѕСЂС‚-РїРѕР»РёС‚РёРє, Р·Р°С‚РµРј:

- РІРєР»СЋС‡Р°РµС‚ `Serilog` console logging;
- Р·Р°РїСѓСЃРєР°РµС‚ desktop РёР»Рё Linux DRM СЂРµР¶РёРј.

## 3. UI-СЃС‚СЂСѓРєС‚СѓСЂР°

- [MainWindow.axaml](../Avalonia3D.Sandbox/MainWindow.axaml):
  - Р»РµРІР°СЏ РїР°РЅРµР»СЊ СѓРїСЂР°РІР»РµРЅРёСЏ (`ModelViewportPanel`);
  - РїСЂР°РІС‹Р№ OpenGL viewport (`SandboxModel3DControl`);
  - overlay СЃРѕ СЃС‚Р°С‚СѓСЃРѕРј РёРјРїРѕСЂС‚Р° Рё quality profile.

- [Controls/ModelViewportPanel.axaml](../Avalonia3D.Sandbox/Controls/ModelViewportPanel.axaml):
  - РІРєР»Р°РґРєРё `Scenes`, `Camera`, `Graphics`, `Animation`.

## 4. РљР»СЋС‡РµРІС‹Рµ runtime-РєР»Р°СЃСЃС‹

### 4.1 `SandboxModel3DControl`

Р¤Р°Р№Р»: [Avalonia3D.Sandbox/Controls/SandboxModel3DControl.cs](../Avalonia3D.Sandbox/Controls/SandboxModel3DControl.cs).

Р РѕР»СЊ:

- РІР»Р°РґРµРµС‚ `SandboxRenderer3D` Рё `Scene3D`;
- СЃРІСЏР·С‹РІР°РµС‚ camera input СЃ РєРѕРЅС‚СЂРѕР»РѕРј;
- РёСЃРїРѕР»РЅСЏРµС‚ РѕС‡РµСЂРµРґСЊ render-thread Р·Р°РґР°С‡;
- РґРµСЂР¶РёС‚ Р°РєС‚РёРІРЅС‹Р№/idle FPS СЂРµР¶РёРјС‹;
- РёРЅРёС†РёРёСЂСѓРµС‚ scene load С‡РµСЂРµР· `SceneLoader`.

РљР»СЋС‡РµРІС‹Рµ СЃРІРѕР№СЃС‚РІР°/РєРѕРјР°РЅРґС‹:

- `SelectedSceneId`, `IsLoading`, `LastLoadError`, `IsRendererReady`;
- `LoadSceneCommand`, `FrameSceneCommand`, `ResetCameraCommand`;
- С‡СѓРІСЃС‚РІРёС‚РµР»СЊРЅРѕСЃС‚СЊ input (`RotationSensitivity`, `PanSensitivity`, `ZoomSensitivity`).

### 4.2 `MainWindowViewModel`

Р¤Р°Р№Р»: [Avalonia3D.Sandbox/ViewModels/MainWindowViewModel.cs](../Avalonia3D.Sandbox/ViewModels/MainWindowViewModel.cs).

Р РѕР»СЊ:

- СЃРІСЏР·С‹РІР°РµС‚ UI Рё runtime РєРѕРјР°РЅРґС‹;
- СѓРїСЂР°РІР»СЏРµС‚ СЃС†РµРЅР°РјРё, РїСЂРѕС„РёР»СЏРјРё, render mode, animation clips;
- РїРѕРєР°Р·С‹РІР°РµС‚ СЃС‚Р°С‚СѓСЃ РёРјРїРѕСЂС‚Р° Рё РєСЌС€РёСЂРѕРІР°РЅРёСЏ;
- СЃРѕРґРµСЂР¶РёС‚ car2-СЃРїРµС†РёС„РёС‡РЅС‹Р№ runtime animator Р±Р»РѕРє.

### 4.3 `SceneCatalog`

Р¤Р°Р№Р»: [Avalonia3D.Sandbox/Scenes/SceneCatalog.cs](../Avalonia3D.Sandbox/Scenes/SceneCatalog.cs).

РђРІС‚РѕРјР°С‚РёС‡РµСЃРєРё РѕР±РЅР°СЂСѓР¶РёРІР°РµС‚ `*.gltf`/`*.glb` РІ [Assets/TestScenes](../Avalonia3D.Sandbox/Assets/TestScenes) СЂРµРєСѓСЂСЃРёРІРЅРѕ.

### 4.4 `SceneLoader` Рё orchestration

Р¤Р°Р№Р»С‹:

- [Services/SceneLoader.cs](../Avalonia3D.Sandbox/Services/SceneLoader.cs)
- [Services/RenderThreadSceneLoadOrchestrator.cs](../Avalonia3D.Sandbox/Services/RenderThreadSceneLoadOrchestrator.cs)
- [Services/SceneLoadService.cs](../Avalonia3D.Sandbox/Services/SceneLoadService.cs)

РњРµС…Р°РЅРёРєР°:

- РєР°Р¶РґС‹Р№ РЅРѕРІС‹Р№ Р·Р°РїСЂРѕСЃ Р·Р°РіСЂСѓР·РєРё РїРѕР»СѓС‡Р°РµС‚ version;
- СѓСЃС‚Р°СЂРµРІС€РёРµ Р·Р°РїСЂРѕСЃС‹ РѕС‚РјРµРЅСЏСЋС‚СЃСЏ РґРѕ apply;
- background prepare СЂР°Р·СЂРµС€РµРЅ С‡РµСЂРµР· `ISceneBackgroundPreparation`;
- РїСЂРёРјРµРЅРµРЅРёРµ СЃС†РµРЅС‹ РІСЃРµРіРґР° РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ РЅР° render thread.

## 5. РЎРµСЂРІРёСЃС‹ Рё РїРѕР»РёС‚РёРєР° Р·Р°РіСЂСѓР·РєРё

РЎРµСЂРІРёСЃС‹:

- `DefaultSceneCameraPolicy` - pre/post-load camera strategy.
- `DefaultSceneDiagnosticsReporter` - Р»РѕРі РёРјРїРѕСЂС‚-РґРёР°РіРЅРѕСЃС‚РёРєРё.
- `CacheCoordinator` + `InMemorySceneAssetCache` + `HybridSceneImportResultCache`.
- `RenderThreadScheduler` - РѕС‡РµСЂРµРґСЊ РґРµР№СЃС‚РІРёР№ Рє OpenGL-РєРѕРЅС‚РµРєСЃС‚Сѓ.

## 6. РЈРїСЂР°РІР»РµРЅРёРµ РіСЂР°С„РёРєРѕР№ Рё РѕС‚Р»Р°РґРєРѕР№

Р§РµСЂРµР· ViewModel/UI РґРѕСЃС‚СѓРїРЅРѕ:

- presets `Low/Medium/High/Ultra/PbrDebugNeutral/Custom`;
- PBR/Unlit/Normals debug render mode;
- emissive texture debug mode;
- PBR debug view mode;
- profile JSON СЂРµРґР°РєС‚РѕСЂ/РїСЂРёРјРµРЅРµРЅРёРµ;
- runtime СЃРјРµРЅР° environment map path.

## 7. РђСЃСЃРµС‚С‹ Рё QA

РџР°РїРєР°: [Avalonia3D.Sandbox/Assets/TestScenes](../Avalonia3D.Sandbox/Assets/TestScenes).

РќР°Р·РЅР°С‡РµРЅРёРµ:

- С‚РµСЃС‚РѕРІС‹Рµ glTF/glb СЃС†РµРЅС‹;
- С‡РµРєР»РёСЃС‚С‹ Рё СЂРµРµСЃС‚СЂС‹ QA (`PBR_QA_CHECKLIST`, `ANIMATION_QA_CHECKLIST`);
- overrides ([material-import-overrides.json](../Avalonia3D.Sandbox/Assets/TestScenes/material-import-overrides.json)).

РЎРєСЂРёРїС‚С‹ РёР· [tools/](../tools/) РёСЃРїРѕР»СЊР·СѓСЋС‚ СЌС‚Сѓ РїР°РїРєСѓ РєР°Рє source of truth РґР»СЏ preflight/regression РїСЂРѕРІРµСЂРѕРє.

## 8. РџСЂР°РєС‚РёС‡РµСЃРєРёР№ СЃС†РµРЅР°СЂРёР№ РѕС‚Р»Р°РґРєРё

1. Р—Р°РїСѓСЃС‚РёС‚СЊ sandbox.
2. РћС‚РєСЂС‹С‚СЊ СЃС†РµРЅСѓ РёР· `Scenes`.
3. РџСЂРѕРІРµСЂРёС‚СЊ `ImportStatusText` Рё СЃС‚Р°С‚СѓСЃ degraded import.
4. РџРµСЂРµРєР»СЋС‡РёС‚СЊ `PBR <-> Unlit`.
5. РР·РјРµРЅРёС‚СЊ РїСЂРѕС„РёР»СЊ РєР°С‡РµСЃС‚РІР°, СЌРєСЃРїРѕР·РёС†РёСЋ, reflection intensity.
6. РџСЂРѕРІРµСЂРёС‚СЊ Р°РЅРёРјР°С†РёРё (play/pause/loop) Рё behavior-РєРѕРјР°РЅРґС‹.



