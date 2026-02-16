# Quick Start (RU)

## 1. РўСЂРµР±РѕРІР°РЅРёСЏ

- `.NET SDK 8.0+`
- Windows/Linux/macOS
- Р”Р»СЏ QA-СЃРєСЂРёРїС‚РѕРІ: `Python 3.10+`

## 2. Р’РѕСЃСЃС‚Р°РЅРѕРІР»РµРЅРёРµ Рё СЃР±РѕСЂРєР°

```bash
dotnet restore Avalonia3D.sln
dotnet build Avalonia3D.sln -c Debug
```

## 3. Р—Р°РїСѓСЃРє sandbox (СЂРµРєРѕРјРµРЅРґСѓРµРјС‹Р№ РІС…РѕРґ)

```bash
dotnet run --project Avalonia3D.Sandbox/Avalonia3D.Sandbox.csproj
```

Р§С‚Рѕ РїСЂРѕРІРµСЂСЏС‚СЊ СЃСЂР°Р·Сѓ РїРѕСЃР»Рµ Р·Р°РїСѓСЃРєР°:
- СЃС†РµРЅС‹ РїРѕСЏРІРёР»РёСЃСЊ РІ `Scenes`;
- РѕС‚СЂРёСЃРѕРІРєР° СЂР°Р±РѕС‚Р°РµС‚ (viewport СЃРїСЂР°РІР°);
- РїРµСЂРµРєР»СЋС‡РµРЅРёРµ РєР°РјРµСЂС‹ `Frame All`, `Reset`, `Toggle Mode`.

## 4. Р—Р°РїСѓСЃРє РѕСЃРЅРѕРІРЅРѕРіРѕ РїСЂРёР»РѕР¶РµРЅРёСЏ

```bash
dotnet run --project Avalonia3D.csproj
```

## 5. Р—Р°РїСѓСЃРє С‚РµСЃС‚РѕРІ

```bash
dotnet test Avalonia3D.Tests/Avalonia3D.Tests.csproj -c Debug
```

## 6. РџР°СЂР°РјРµС‚СЂС‹ РёРјРїРѕСЂС‚Р° РјР°С‚РµСЂРёР°Р»РѕРІ Рё РІР°Р»РёРґР°С†РёРё

CLI-Р°СЂРіСѓРјРµРЅС‚С‹:
- `--material-alpha-import=<strict|balanced|legacy>`
- `--material-import-overrides=<path>`

РџРµСЂРµРјРµРЅРЅС‹Рµ РѕРєСЂСѓР¶РµРЅРёСЏ:
- `AVALONIA3D_MATERIAL_ALPHA_IMPORT`
- `AVALONIA3D_MATERIAL_IMPORT_OVERRIDES`

## 7. QA-СЃРєСЂРёРїС‚С‹ Р°СЃСЃРµС‚РѕРІ

РџСЂРѕРІРµСЂРєР° glTF preflight:
```bash
python tools/validate_gltf_assets.py
```

РџСЂРѕРІРµСЂРєР° PBR-СЂРµРіСЂРµСЃСЃРёР№:
```bash
python tools/validate_pbr_snapshot.py
```

## 8. Linux DRM Р·Р°РїСѓСЃРє (РѕРїС†РёРѕРЅР°Р»СЊРЅРѕ)

РџРѕРґРґРµСЂР¶РёРІР°РµРјС‹Рµ Р°СЂРіСѓРјРµРЅС‚С‹:
- `-card=<cardX>` (РїСЂРёРјРµСЂ: `-card=card1`)
- `-resolution=<width>x<height>` (РїСЂРёРјРµСЂ: `-resolution=1920x1080`)

## 9. Р“РґРµ С‡РёС‚Р°С‚СЊ РґР°Р»СЊС€Рµ

- [docs/README.md](docs/README.md)
- [docs/ARCHITECTURE.ru.md](docs/ARCHITECTURE.ru.md)
- [docs/PROJECT_AVALONIA3D.ru.md](docs/PROJECT_AVALONIA3D.ru.md)
- [docs/PROJECT_SANDBOX.ru.md](docs/PROJECT_SANDBOX.ru.md)
- [docs/PROJECT_TESTS.ru.md](docs/PROJECT_TESTS.ru.md)

