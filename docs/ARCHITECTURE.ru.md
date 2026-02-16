# Архитектура Avalonia3D (RU)

## 1. Общая схема

Решение состоит из трех проектов:

- `Avalonia3D` - ядро движка.
- `Avalonia3D.Sandbox` - UI-хост, интерактивная среда тестирования.
- `Avalonia3D.Tests` - модульные/интеграционные тесты доменной логики.

Поток данных в рантайме:

1. UI/сцена инициирует загрузку (`SceneLoader`, `RenderThreadSceneLoadOrchestrator`).
2. Импортер (`GltfSceneImporter`) читает glTF/glb, применяет политику валидации и material policy.
3. `Scene3D` получает `SceneImportResult`, перестраивает `SceneGraph`, регистрирует клипы анимации.
4. Рендер-контрол (`SandboxModel3DControl`) исполняет render-thread queue и вызывает `RenderPipeline`.
5. `RenderPipeline` выполняет набор pass-ов (`Shadow`, `Environment`, `Forward`, post-effects).

## 2. Точки входа

- [Program.cs](../Program.cs) � `Avalonia3D` � [Avalonia3D.Sandbox/Program.cs](../Avalonia3D.Sandbox/Program.cs):
  - применяют политики импорта/alpha override;
  - выбирают desktop startup или Linux DRM startup;
  - конфигурируют Avalonia app builder.

- [App.axaml.cs](../App.axaml.cs) � [Avalonia3D.Sandbox/App.axaml.cs](../Avalonia3D.Sandbox/App.axaml.cs):
  - инициализация root window (`MainWindow`).

## 3. Слои системы

### 3.1 Domain Scene Layer

Ключевой класс: [Model/Scene3D.cs](../Model/Scene3D.cs).

Ответственности:

- хранение актуального `SceneGraph`;
- управление поведениями (`ISceneBehavior`) и модулями (`ISceneModule`);
- управление анимациями через `AnimatorComponent`;
- диспетчеризация команд (`SceneCommandBus`) для runtime-поведений;
- хранение render mode, graphics profile и import report.

### 3.2 Import Layer

Ключевые файлы:

- [Loaders/GltfSceneImporter.cs](../Loaders/GltfSceneImporter.cs)
- [Loaders/ModelLoader.cs](../Loaders/ModelLoader.cs)
- [Loaders/Policies/DefaultMaterialImportPolicy.cs](../Loaders/Policies/DefaultMaterialImportPolicy.cs)
- [Loaders/MaterialAlphaImportPolicy.cs](../Loaders/MaterialAlphaImportPolicy.cs)

Возможности:

- strict/relaxed стратегия валидации glTF;
- fallback-режим деградированной загрузки вместо аварийного падения;
- материал-политика по alpha mode и эвристики по прозрачности текстур;
- precomputed material map и texture decode cache;
- извлечение каналов анимации TRS/morph/material/texture-transform.

### 3.3 Render Layer

Ключевые файлы:

- [Rendering/RenderPipeline.cs](../Rendering/RenderPipeline.cs)
- [Rendering/RenderPipelineFactory.cs](../Rendering/RenderPipelineFactory.cs)
- [Rendering/RenderResourceManager.cs](../Rendering/RenderResourceManager.cs)
- [Rendering/ForwardPass.cs](../Rendering/ForwardPass.cs)
- [Rendering/ShadowPass.cs](../Rendering/ShadowPass.cs)
- [Rendering/EnvironmentLightingPass.cs](../Rendering/EnvironmentLightingPass.cs)
- [Rendering/BloomPass.cs](../Rendering/BloomPass.cs)
- [Rendering/PostEffectsPass.cs](../Rendering/PostEffectsPass.cs)

Процесс:

1. Сбор mesh-объектов из дерева.
2. Frustum culling.
3. Разделение на opaque/transparent.
4. Сортировка transparent back-to-front.
5. Выполнение pass-ов в порядке, заданном `RenderPipelineFactory`.

### 3.4 Shader Layer

Ключевые файлы:

- [Rendering/ShaderSelectionPolicy.cs](../Rendering/ShaderSelectionPolicy.cs)
- [Rendering/RuntimePbrShaderFactory.cs](../Rendering/RuntimePbrShaderFactory.cs)
- [Shaders/PbrShaderSourceBuilder.cs](../Shaders/PbrShaderSourceBuilder.cs)
- [Shaders/ShaderIds.cs](../Shaders/ShaderIds.cs)

Логика:

- приоритет explicit material shader > material shader id > режим сцены > PBR variant;
- при необходимости runtime-компиляция варианта PBR;
- fallback chain при неуспехе компиляции (редуцированные фичи/базовый PBR/default).

### 3.5 Interaction/Behavior Layer

Ключевые файлы:

- [Interaction/CameraController/*](../Interaction/CameraController/)
- [Interaction/Behaviors/DoorBehavior.cs](../Interaction/Behaviors/DoorBehavior.cs)
- [Interaction/Behaviors/WheelRotationBehavior.cs](../Interaction/Behaviors/WheelRotationBehavior.cs)

Механика:

- камера обрабатывает мышь/клавиатуру и mode switching;
- behavior-объекты получают команды (`open/close/toggle`) через `SceneCommandBus`;
- часть behaviors также обновляется каждый кадр (`IUpdatableBehavior`).

### 3.6 Sandbox Orchestration Layer

Ключевые файлы:

- [Avalonia3D.Sandbox/Controls/SandboxModel3DControl.cs](../Avalonia3D.Sandbox/Controls/SandboxModel3DControl.cs)
- [Avalonia3D.Sandbox/Services/RenderThreadSceneLoadOrchestrator.cs](../Avalonia3D.Sandbox/Services/RenderThreadSceneLoadOrchestrator.cs)
- [Avalonia3D.Sandbox/Services/SceneLoadService.cs](../Avalonia3D.Sandbox/Services/SceneLoadService.cs)
- [Avalonia3D.Sandbox/ViewModels/MainWindowViewModel.cs](../Avalonia3D.Sandbox/ViewModels/MainWindowViewModel.cs)

Назначение:

- потокобезопасная загрузка сцен с отменой устаревших запросов;
- фоновые prepare-шаги перед apply на render thread;
- кэширование и диагностика сцен;
- управление профилями графики, отладочными режимами PBR и клипами анимаций.

## 4. Жизненный цикл сцены

1. Выбор сцены в UI.
2. `SceneCatalog` разрешает id в `ISandboxScene`.
3. `RenderThreadSceneLoadOrchestrator.Load(...)`:
   - опционально выгружает текущую сцену;
   - выполняет background prepare;
   - применяет только последний актуальный запрос.
4. `SceneLoadService.LoadNow(...)`:
   - применяет camera policy;
   - загружает сцену или prepared payload;
   - публикует диагностику и событие `SceneChanged`.
5. ViewModel получает `SceneLoaded`, обновляет UI-состояние.

## 5. Графические профили

[Rendering/GraphicsProfile.cs](../Rendering/GraphicsProfile.cs) определяет:

- `Low`, `Medium`, `High`, `Ultra`, `PbrDebugNeutral`;
- shadow map size, post effects, bloom, reflections, exposure/IBL, background;
- сериализацию/десериализацию профиля для UI-редактора JSON.

## 6. Память и кеши

Ключевые механики:

- [Memory/MemoryManager.cs](../Memory/MemoryManager.cs) - мягкая очистка/режимы под крупные ассеты.
- `ModelLoader`:
  - LRU texture decode cache;
  - material index map cache;
  - persisted texture bytes cache.
- `GltfSceneImporter`:
  - model cache с TTL/size limit;
  - очистка по memory pressure policy.

## 7. Диагностика и качество

- `Scene3D.LastImportReport` хранит статус (Success/Degraded) и список проблем.
- [Rendering/Diagnostics/MaterialRenderDiagnostics.cs](../Rendering/Diagnostics/MaterialRenderDiagnostics.cs) для PBR-трассировки материалов.
- Python preflight:
  - [tools/validate_gltf_assets.py](../tools/validate_gltf_assets.py)
  - [tools/validate_pbr_snapshot.py](../tools/validate_pbr_snapshot.py)

## 8. Тестовая стратегия

`Avalonia3D.Tests` покрывает:

- выбор шейдеров и fallback-пути;
- формирование render pipeline;
- scene load orchestration и cache semantics;
- import policy/texture policy;
- behavior integration (door/wheel/command bus);
- camera controller и frame-state контракты.




