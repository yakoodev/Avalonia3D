# РџСЂРѕРµРєС‚ `Avalonia3D.Tests` (RU)

## 1. РќР°Р·РЅР°С‡РµРЅРёРµ

`Avalonia3D.Tests` - xUnit-РїСЂРѕРµРєС‚ РґР»СЏ РїСЂРѕРІРµСЂРєРё РґРѕРјРµРЅРЅРѕР№ Р»РѕРіРёРєРё Рё РєРѕРЅС‚СЂР°РєС‚РѕРІ РїРѕРґСЃРёСЃС‚РµРј Р±РµР· Р·Р°РїСѓСЃРєР° РїРѕР»РЅРѕРіРѕ UI.

РџР»Р°С‚С„РѕСЂРјР°: `net8.0`.

РћСЃРЅРѕРІРЅС‹Рµ РїР°РєРµС‚С‹:

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`

## 2. РћР±Р»Р°СЃС‚Рё РїРѕРєСЂС‹С‚РёСЏ

### 2.1 Shader selection Рё PBR-РІР°СЂРёР°РЅС‚С‹

Р¤Р°Р№Р»: [Avalonia3D.Tests/ShaderSelectionTests.cs](../Avalonia3D.Tests/ShaderSelectionTests.cs).

РџСЂРѕРІРµСЂСЏРµС‚:

- РїСЂРёРѕСЂРёС‚РµС‚С‹ РІС‹Р±РѕСЂР° С€РµР№РґРµСЂР°;
- РіРµРЅРµСЂР°С†РёСЋ PBR feature id;
- fallback РїСЂРё РїСЂРѕРІР°Р»Рµ runtime-РєРѕРјРїРёР»СЏС†РёРё;
- РїРѕРІРµРґРµРЅРёРµ РґР»СЏ transmission/extension-С„РёС‡.

### 2.2 Render pipeline composition

Р¤Р°Р№Р»: [Avalonia3D.Tests/RenderPipelineFactoryTests.cs](../Avalonia3D.Tests/RenderPipelineFactoryTests.cs).

РџСЂРѕРІРµСЂСЏРµС‚:

- РїСЂР°РІРёР»СЊРЅС‹Р№ СЃРѕСЃС‚Р°РІ Рё РїРѕСЂСЏРґРѕРє pass-РѕРІ РїРѕ РїСЂРѕС„РёР»СЏРј;
- РІРєР»СЋС‡РµРЅРёРµ/РІС‹РєР»СЋС‡РµРЅРёРµ environment pass;
- СѓСЃР»РѕРІРёСЏ РїРѕСЏРІР»РµРЅРёСЏ `BloomPass` Рё `PostEffectsPass`.

### 2.3 Scene loading orchestration

Р¤Р°Р№Р»: [Avalonia3D.Tests/SceneLoaderTests.cs](../Avalonia3D.Tests/SceneLoaderTests.cs).

РџСЂРѕРІРµСЂСЏРµС‚:

- РѕС‚Р»РѕР¶РµРЅРЅСѓСЋ Р·Р°РіСЂСѓР·РєСѓ РґРѕ РіРѕС‚РѕРІРЅРѕСЃС‚Рё СЂРµРЅРґРµСЂР°;
- РѕС‚РјРµРЅСѓ СЃС‚Р°СЂС‹С… Р·Р°РїСЂРѕСЃРѕРІ РїСЂРё СЃРµСЂРёРё Р·Р°РіСЂСѓР·РѕРє;
- РїСЂР°РІРёР»СЊРЅРѕРµ С‡РёСЃР»Рѕ render-thread apply С€Р°РіРѕРІ;
- cache hit/miss Рё camera policy РІС‹Р·РѕРІС‹.

### 2.4 Behavior integration

Р¤Р°Р№Р»: [Avalonia3D.Tests/BehaviorIntegrationTests.cs](../Avalonia3D.Tests/BehaviorIntegrationTests.cs).

РџСЂРѕРІРµСЂСЏРµС‚:

- dispatch РєРѕРјР°РЅРґ РІ `DoorBehavior`;
- runtime fallback rotation РґР»СЏ РґРІРµСЂРµР№ РїСЂРё РѕС‚СЃСѓС‚СЃС‚РІРёРё РєР»РёРїРѕРІ;
- wheel rotation behavior РїРѕ target key mode;
- СЃРѕР±С‹С‚РёСЏ Р·Р°РІРµСЂС€РµРЅРёСЏ РєР»РёРїРѕРІ `AnimatorComponent`.

### 2.5 Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅС‹Рµ Р±Р»РѕРєРё

РџРѕ РёРјРµРЅР°Рј С„Р°Р№Р»РѕРІ С‚Р°РєР¶Рµ РїРѕРєСЂС‹РІР°СЋС‚СЃСЏ:

- material alpha/import policy;
- texture semantics/color management/decode policy;
- import diagnostics;
- camera controller, frame-state Рё resource manager.

## 3. РљР°Рє Р·Р°РїСѓСЃРєР°С‚СЊ

РР· РєРѕСЂРЅСЏ СЂРµРїРѕР·РёС‚РѕСЂРёСЏ:

```bash
dotnet test Avalonia3D.Tests/Avalonia3D.Tests.csproj -c Debug
```

РџРѕР»РЅС‹Р№ solution-СЂР°РЅ:

```bash
dotnet test Avalonia3D.sln -c Debug
```

## 4. РџСЂР°РєС‚РёРєРё РЅР°РїРёСЃР°РЅРёСЏ С‚РµСЃС‚РѕРІ РІ СЌС‚РѕРј РїСЂРѕРµРєС‚Рµ

- Р°РєС†РµРЅС‚ РЅР° deterministic domain behavior;
- РјРёРЅРёРјСѓРј UI-Р·Р°РІРёСЃРёРјРѕСЃС‚РµР№;
- РёР·РѕР»СЏС†РёСЏ С‡РµСЂРµР· test doubles (РЅР°РїСЂРёРјРµСЂ scheduler/factory stubs);
- coverage РєР»СЋС‡РµРІС‹С… fallback-РїСѓС‚РµР№, Р° РЅРµ С‚РѕР»СЊРєРѕ happy path.

## 5. РћРіСЂР°РЅРёС‡РµРЅРёСЏ

- С‚РµСЃС‚С‹ РЅРµ Р·Р°РјРµРЅСЏСЋС‚ СЂСѓС‡РЅСѓСЋ РІРёР·СѓР°Р»СЊРЅСѓСЋ РїСЂРѕРІРµСЂРєСѓ РєР°С‡РµСЃС‚РІР° PBR/Р°РЅРёРјР°С†РёРё;
- РґР»СЏ asset-СЃРїРµС†РёС„РёС‡РЅС‹С… РїСЂРѕРІРµСЂРѕРє РґРѕРїРѕР»РЅРёС‚РµР»СЊРЅРѕ Р·Р°РїСѓСЃРєР°СЋС‚СЃСЏ Python QA-СЃРєСЂРёРїС‚С‹ РёР· [tools/](../tools/).


