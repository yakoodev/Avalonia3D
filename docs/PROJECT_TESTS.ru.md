# Проект `Avalonia3D.Tests` (RU)

## 1. Назначение

`Avalonia3D.Tests` - xUnit-проект для проверки доменной логики и контрактов подсистем без запуска полного UI.

Платформа: `net8.0`.

Основные пакеты:

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`

## 2. Области покрытия

### 2.1 Shader selection и PBR-варианты

Файл: `Avalonia3D.Tests/ShaderSelectionTests.cs`.

Проверяет:

- приоритеты выбора шейдера;
- генерацию PBR feature id;
- fallback при провале runtime-компиляции;
- поведение для transmission/extension-фич.

### 2.2 Render pipeline composition

Файл: `Avalonia3D.Tests/RenderPipelineFactoryTests.cs`.

Проверяет:

- правильный состав и порядок pass-ов по профилям;
- включение/выключение environment pass;
- условия появления `BloomPass` и `PostEffectsPass`.

### 2.3 Scene loading orchestration

Файл: `Avalonia3D.Tests/SceneLoaderTests.cs`.

Проверяет:

- отложенную загрузку до готовности рендера;
- отмену старых запросов при серии загрузок;
- правильное число render-thread apply шагов;
- cache hit/miss и camera policy вызовы.

### 2.4 Behavior integration

Файл: `Avalonia3D.Tests/BehaviorIntegrationTests.cs`.

Проверяет:

- dispatch команд в `DoorBehavior`;
- runtime fallback rotation для дверей при отсутствии клипов;
- wheel rotation behavior по target key mode;
- события завершения клипов `AnimatorComponent`.

### 2.5 Дополнительные блоки

По именам файлов также покрываются:

- material alpha/import policy;
- texture semantics/color management/decode policy;
- import diagnostics;
- camera controller, frame-state и resource manager.

## 3. Как запускать

Из корня репозитория:

```bash
dotnet test Avalonia3D.Tests/Avalonia3D.Tests.csproj -c Debug
```

Полный solution-ран:

```bash
dotnet test Avalonia3D.sln -c Debug
```

## 4. Практики написания тестов в этом проекте

- акцент на deterministic domain behavior;
- минимум UI-зависимостей;
- изоляция через test doubles (например scheduler/factory stubs);
- coverage ключевых fallback-путей, а не только happy path.

## 5. Ограничения

- тесты не заменяют ручную визуальную проверку качества PBR/анимации;
- для asset-специфичных проверок дополнительно запускаются Python QA-скрипты из `tools/`.
