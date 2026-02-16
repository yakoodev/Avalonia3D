# Quick Start (EN)

## 1. Requirements

- `.NET SDK 8.0+`
- Windows/Linux/macOS
- For QA scripts: `Python 3.10+`

## 2. Restore and build

```bash
dotnet restore Avalonia3D.sln
dotnet build Avalonia3D.sln -c Debug
```

## 3. Run sandbox (recommended entry point)

```bash
dotnet run --project Avalonia3D.Sandbox/Avalonia3D.Sandbox.csproj
```

Initial smoke check:
- scene list is populated in `Scenes`;
- rendering works in the right viewport;
- camera commands (`Frame All`, `Reset`, `Toggle Mode`) work.

## 4. Run core app

```bash
dotnet run --project Avalonia3D.csproj
```

## 5. Run tests

```bash
dotnet test Avalonia3D.Tests/Avalonia3D.Tests.csproj -c Debug
```

## 6. Material import and validation options

CLI arguments:
- `--material-alpha-import=<strict|balanced|legacy>`
- `--material-import-overrides=<path>`

Environment variables:
- `AVALONIA3D_MATERIAL_ALPHA_IMPORT`
- `AVALONIA3D_MATERIAL_IMPORT_OVERRIDES`

## 7. Asset QA scripts

glTF preflight:
```bash
python tools/validate_gltf_assets.py
```

PBR regression snapshot checks:
```bash
python tools/validate_pbr_snapshot.py
```

## 8. Linux DRM startup (optional)

Supported args:
- `-card=<cardX>` (example: `-card=card1`)
- `-resolution=<width>x<height>` (example: `-resolution=1920x1080`)

## 9. Read more

- `docs/README.md`
- `docs/ARCHITECTURE.en.md`
- `docs/PROJECT_AVALONIA3D.en.md`
- `docs/PROJECT_SANDBOX.en.md`
- `docs/PROJECT_TESTS.en.md`
