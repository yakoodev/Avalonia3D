# Project `Avalonia3D.Tests` (EN)

## 1. Purpose

`Avalonia3D.Tests` is the xUnit project validating domain-level behavior and subsystem contracts without launching full UI.

Target framework: `net8.0`.

Main packages:

- `Microsoft.NET.Test.Sdk`
- `xunit`
- `xunit.runner.visualstudio`
- `coverlet.collector`

## 2. Coverage areas

### 2.1 Shader selection and PBR variants

File: [Avalonia3D.Tests/ShaderSelectionTests.cs](../Avalonia3D.Tests/ShaderSelectionTests.cs).

Validates:

- shader selection priority rules;
- PBR feature-id generation;
- fallback behavior after runtime shader compile failure;
- transmission/extension feature handling.

### 2.2 Render pipeline composition

File: [Avalonia3D.Tests/RenderPipelineFactoryTests.cs](../Avalonia3D.Tests/RenderPipelineFactoryTests.cs).

Validates:

- expected pass ordering by profile;
- environment pass enable/disable conditions;
- `BloomPass` and `PostEffectsPass` inclusion rules.

### 2.3 Scene loading orchestration

File: [Avalonia3D.Tests/SceneLoaderTests.cs](../Avalonia3D.Tests/SceneLoaderTests.cs).

Validates:

- deferred loads before renderer readiness;
- superseded load cancellation behavior;
- render-thread apply step counts;
- cache hit/miss semantics and camera policy invocation.

### 2.4 Behavior integration

File: [Avalonia3D.Tests/BehaviorIntegrationTests.cs](../Avalonia3D.Tests/BehaviorIntegrationTests.cs).

Validates:

- command dispatch to `DoorBehavior`;
- runtime door rotation fallback when clips are missing;
- wheel behavior resolution by target-key mode;
- `AnimatorComponent` clip completion events.

### 2.5 Additional areas

Based on test files, suite also covers:

- material alpha/import policy;
- texture semantics/color/decode policy;
- import diagnostics;
- camera controller, frame state, and resource manager behavior.

## 3. How to run

From repository root:

```bash
dotnet test Avalonia3D.Tests/Avalonia3D.Tests.csproj -c Debug
```

Whole solution:

```bash
dotnet test Avalonia3D.sln -c Debug
```

## 4. Test design patterns used

- deterministic domain behavior focus;
- minimal UI dependency;
- isolation with test doubles (scheduler/factory stubs, etc.);
- explicit fallback-path validation, not only happy path checks.

## 5. Limits

- tests do not replace manual visual QA for PBR/animation quality;
- asset-specific regression checks are complemented by Python scripts under [tools/](../tools/).


