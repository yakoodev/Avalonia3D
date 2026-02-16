# Проект `Avalonia3D.Sandbox` (RU)

## 1. Назначение

`Avalonia3D.Sandbox` - рабочая оболочка для:

- интерактивной проверки рендера;
- просмотра и переключения glTF/glb сцен;
- настройки качества графики в рантайме;
- проверки анимаций и behavior-команд;
- диагностики проблем импорта.

## 2. Старт приложения

Файл: `Avalonia3D.Sandbox/Program.cs`.

Делает то же базовое конфигурирование импорт-политик, затем:

- включает `Serilog` console logging;
- запускает desktop или Linux DRM режим.

## 3. UI-структура

- `MainWindow.axaml`:
  - левая панель управления (`ModelViewportPanel`);
  - правый OpenGL viewport (`SandboxModel3DControl`);
  - overlay со статусом импорта и quality profile.

- `Controls/ModelViewportPanel.axaml`:
  - вкладки `Scenes`, `Camera`, `Graphics`, `Animation`.

## 4. Ключевые runtime-классы

### 4.1 `SandboxModel3DControl`

Файл: `Avalonia3D.Sandbox/Controls/SandboxModel3DControl.cs`.

Роль:

- владеет `SandboxRenderer3D` и `Scene3D`;
- связывает camera input с контролом;
- исполняет очередь render-thread задач;
- держит активный/idle FPS режимы;
- инициирует scene load через `SceneLoader`.

Ключевые свойства/команды:

- `SelectedSceneId`, `IsLoading`, `LastLoadError`, `IsRendererReady`;
- `LoadSceneCommand`, `FrameSceneCommand`, `ResetCameraCommand`;
- чувствительность input (`RotationSensitivity`, `PanSensitivity`, `ZoomSensitivity`).

### 4.2 `MainWindowViewModel`

Файл: `Avalonia3D.Sandbox/ViewModels/MainWindowViewModel.cs`.

Роль:

- связывает UI и runtime команды;
- управляет сценами, профилями, render mode, animation clips;
- показывает статус импорта и кэширования;
- содержит car2-специфичный runtime animator блок.

### 4.3 `SceneCatalog`

Файл: `Avalonia3D.Sandbox/Scenes/SceneCatalog.cs`.

Автоматически обнаруживает `*.gltf`/`*.glb` в `Assets/TestScenes` рекурсивно.

### 4.4 `SceneLoader` и orchestration

Файлы:

- `Services/SceneLoader.cs`
- `Services/RenderThreadSceneLoadOrchestrator.cs`
- `Services/SceneLoadService.cs`

Механика:

- каждый новый запрос загрузки получает version;
- устаревшие запросы отменяются до apply;
- background prepare разрешен через `ISceneBackgroundPreparation`;
- применение сцены всегда выполняется на render thread.

## 5. Сервисы и политика загрузки

Сервисы:

- `DefaultSceneCameraPolicy` - pre/post-load camera strategy.
- `DefaultSceneDiagnosticsReporter` - лог импорт-диагностики.
- `CacheCoordinator` + `InMemorySceneAssetCache` + `HybridSceneImportResultCache`.
- `RenderThreadScheduler` - очередь действий к OpenGL-контексту.

## 6. Управление графикой и отладкой

Через ViewModel/UI доступно:

- presets `Low/Medium/High/Ultra/PbrDebugNeutral/Custom`;
- PBR/Unlit/Normals debug render mode;
- emissive texture debug mode;
- PBR debug view mode;
- profile JSON редактор/применение;
- runtime смена environment map path.

## 7. Ассеты и QA

Папка: `Avalonia3D.Sandbox/Assets/TestScenes`.

Назначение:

- тестовые glTF/glb сцены;
- чеклисты и реестры QA (`PBR_QA_CHECKLIST`, `ANIMATION_QA_CHECKLIST`);
- overrides (`material-import-overrides.json`).

Скрипты из `tools/` используют эту папку как source of truth для preflight/regression проверок.

## 8. Практический сценарий отладки

1. Запустить sandbox.
2. Открыть сцену из `Scenes`.
3. Проверить `ImportStatusText` и статус degraded import.
4. Переключить `PBR <-> Unlit`.
5. Изменить профиль качества, экспозицию, reflection intensity.
6. Проверить анимации (play/pause/loop) и behavior-команды.
