# Проект `Avalonia3D` (RU)

## 1. Назначение

`Avalonia3D` - это ядро 3D-движка:

- модель сцены (`Scene3D`, `SceneGraph`, `SceneNode`);
- импорт glTF/glb (`GltfSceneImporter`, `ModelLoader`);
- рендер-пайплайн и OpenGL-ресурсы;
- системы анимации и runtime-поведения;
- шейдерная подсистема PBR/Unlit/Debug.

## 2. Точка запуска

Файл: `Program.cs`.

Перед стартом UI:

- `ImportValidationConfiguration.Configure(...)`
- `MaterialAlphaImportConfiguration.Configure(...)`
- `MaterialImportOverrideConfiguration.ConfigureFromPath(...)`

Далее:

- desktop startup через `StartWithClassicDesktopLifetime`;
- на Linux возможен DRM startup с `-card=` и `-resolution=`.

## 3. Ключевые пакеты

В `Avalonia3D.csproj` подключены:

- Avalonia UI (`Avalonia`, `Avalonia.Desktop`, `Avalonia.Skia`, и т.д.);
- OpenGL API (`Silk.NET.OpenGL`, `Silk.NET.OpenGLES`);
- glTF стек (`SharpGLTF.Core`, `SharpGLTF.Runtime`, `SharpGLTF.Toolkit`);
- image decode (`SixLabors.ImageSharp`);
- логирование (`Serilog`).

## 4. Основные подсистемы

### 4.1 Scene и domain model

Папка: `Model/`.

Ключевые типы:

- `Scene3D` - центральный runtime-контейнер.
- `SceneGraph` / `SceneNode` - иерархия узлов.
- `MeshObject`, `MeshGroup`, `Material`, `TextureData`.
- `EnvironmentLightingSettings` - runtime-параметры IBL/окружения.

### 4.2 Импорт glTF

Папка: `Loaders/`.

Ключевые типы:

- `GltfSceneImporter`:
  - чтение `ModelRoot`;
  - fallback из strict в relaxed validation;
  - построение `SceneImportResult`.
- `ModelLoader`:
  - разбор геометрии/материалов/текстур;
  - material policy;
  - texture decode + resize + cache.

Политики:

- `ImportValidationConfiguration` - strict/relaxed.
- `MaterialAlphaImportConfiguration` - `strict|balanced|legacy`.
- `MaterialImportOverrideConfiguration` - asset/material overrides из JSON.

### 4.3 Рендер

Папка: `Rendering/`.

Ключевые компоненты:

- `RenderPipeline` - сбор видимых объектов, culling, сортировка прозрачных.
- `RenderPipelineFactory` - состав pass-ов по `GraphicsProfile`.
- `RenderResourceManager` - GPU-буферы/текстуры и кеш геометрии.
- Pass-ы:
  - `ShadowPass`
  - `EnvironmentLightingPass`
  - `ForwardPass`
  - `BloomPass`
  - `PostEffectsPass`

### 4.4 Шейдеры

Папки: `Shaders/`, `Rendering/ShaderSelectionPolicy.cs`.

Особенности:

- статический registry (`ShaderRegistry`);
- feature-based PBR shader ids (`ShaderIds.CreatePbrVariantId(...)`);
- runtime генерация PBR-вариантов при недостающем статическом шейдере;
- fallback к базовому PBR/default.

### 4.5 Анимация

Папка: `Animation/`.

Ключевые элементы:

- `Animator`, `AnimatorComponent`;
- `AnimationClip`, `AnimationChannel`, keyframe/interpolation;
- binding-классы для node/material/texture transform целей;
- поддержка morph-driven emission логики.

### 4.6 Поведения и команды

Папка: `Interaction/Behaviors/`.

Ключевые элементы:

- `SceneCommand`, `SceneCommandBus`;
- `DoorBehavior` (включая runtime rotation fallback);
- `WheelRotationBehavior`.

## 5. GraphicsProfile и качество

Файл: `Rendering/GraphicsProfile.cs`.

Профили:

- `Low`, `Medium`, `High`, `Ultra`, `PbrDebugNeutral`.

Покрывают:

- MSAA;
- shadow map size;
- post effects flags;
- bloom настройки;
- reflections and environment map;
- PBR tuning (exposure, IBL intensity, clamps).

## 6. Память и кеширование

- `Memory/MemoryManager.cs` - soft cleanup, режимы под крупные ассеты.
- `ModelLoader`:
  - texture decode LRU cache;
  - material index map cache;
  - persisted texture cache.
- `GltfSceneImporter`:
  - cache импортированных моделей с trim по размеру/возрасту.

## 7. Расширение и интеграция

Типичные extension points:

- новые `ISceneModule`;
- новые `ISceneBehavior`/`IUpdatableBehavior`;
- новые render passes через расширение `RenderPipelineFactory`;
- новые shader-варианты через `ShaderRegistry` и runtime factory;
- кастомный material import policy.
