# Quick Start (RU)

## 1. Требования

- `.NET SDK 8.0+`
- Windows/Linux/macOS
- Для QA-скриптов: `Python 3.10+`

## 2. Восстановление и сборка

```bash
dotnet restore Avalonia3D.sln
dotnet build Avalonia3D.sln -c Debug
```

## 3. Запуск sandbox (рекомендуемый вход)

```bash
dotnet run --project Avalonia3D.Sandbox/Avalonia3D.Sandbox.csproj
```

Что проверять сразу после запуска:
- сцены появились в `Scenes`;
- отрисовка работает (viewport справа);
- переключение камеры `Frame All`, `Reset`, `Toggle Mode`.

## 4. Запуск основного приложения

```bash
dotnet run --project Avalonia3D.csproj
```

## 5. Запуск тестов

```bash
dotnet test Avalonia3D.Tests/Avalonia3D.Tests.csproj -c Debug
```

## 6. Параметры импорта материалов и валидации

CLI-аргументы:
- `--material-alpha-import=<strict|balanced|legacy>`
- `--material-import-overrides=<path>`

Переменные окружения:
- `AVALONIA3D_MATERIAL_ALPHA_IMPORT`
- `AVALONIA3D_MATERIAL_IMPORT_OVERRIDES`

## 7. QA-скрипты ассетов

Проверка glTF preflight:
```bash
python tools/validate_gltf_assets.py
```

Проверка PBR-регрессий:
```bash
python tools/validate_pbr_snapshot.py
```

## 8. Linux DRM запуск (опционально)

Поддерживаемые аргументы:
- `-card=<cardX>` (пример: `-card=card1`)
- `-resolution=<width>x<height>` (пример: `-resolution=1920x1080`)

## 9. Где читать дальше

- `docs/README.md`
- `docs/ARCHITECTURE.ru.md`
- `docs/PROJECT_AVALONIA3D.ru.md`
- `docs/PROJECT_SANDBOX.ru.md`
- `docs/PROJECT_TESTS.ru.md`
