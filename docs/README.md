# Material Import Policy

Кратко: вся логика принятия решений по импорту материалов находится в `IMaterialImportPolicy` и его реализации `DefaultMaterialImportPolicy`.

## Где находится источник правды

- Интерфейс policy: `Loaders/Policies/IMaterialImportPolicy.cs`.
- Дефолтная реализация: `Loaders/Policies/DefaultMaterialImportPolicy.cs`.
- Контекст вызова policy (asset, material, animation, profile): `MaterialImportPolicyContext` в `Loaders/Policies/MaterialImportOverrideConfiguration.cs`.

## Профили

Поддерживаются профили `strict`, `balanced`, `legacy` (см. `MaterialAlphaImportProfile`).

- `strict`: BLEND сохраняется максимально консервативно.
- `balanced`: компромисс между визуальной корректностью и уменьшением лишней прозрачности.
- `legacy`: историческое поведение, но **BLEND + emissive не схлопывается в Opaque** (контракт для emissive-ассетов).

Профиль задаётся через:

- аргумент `--material-alpha-import=<strict|balanced|legacy>`
- переменную окружения `AVALONIA3D_MATERIAL_ALPHA_IMPORT`

## Asset-level и material-level overrides

Конфиг overrides задаётся через:

- аргумент `--material-import-overrides=<path>`
- переменную окружения `AVALONIA3D_MATERIAL_IMPORT_OVERRIDES`

Поддерживаются два уровня:

1. Asset-level (ключ — путь к `.gltf/.glb`).
2. Material-level (внутри `materials`, ключ — имя материала), приоритетнее asset-level.

Поддерживаемые поля override:

- `alphaProfile`
- `forceAlphaMode`
- `preserveBlendWithoutAlphaSignalForEmissive`
- `forceTextureTransparencySignal`

## Decision-лог

`DefaultMaterialImportPolicy` пишет debug-лог с reason code решения alpha mode.
Это единственная точка, где следует расширять правила, чтобы изменения не расползались по loader-коду.

## Контракт для animated emissive

Для анимированных emissive-материалов (контекст `IsAnimatedMaterial=true`) действует контракт: BLEND сохраняется и не деградирует в Opaque при emissive-сигнале, независимо от профиля.
