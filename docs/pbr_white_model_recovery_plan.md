# План исправления проблемы «модель белая/пересвеченная в PBR»

## Контекст по текущим логам

По предоставленному логу видно несколько важных маркеров:

1. Для `.glb` вызывается `GltfDependencyInspector.ReadExternalUris`, который пытается парсить бинарный GLB как JSON и падает с `JsonReaderException ('g' is an invalid start of a value)`. Это не корень «белой модели», но создаёт шум и может скрывать реальные сигналы.
2. При загрузке текстур многократно выставляется флаг `BaseColorMissingSrgbDecode` (и для emissive: `EmissiveMissingSrgbDecode`). Это сильный индикатор проблемы цветового пространства.
3. Текстуры реально загружены и успешно биндинятся в GL (`willBind=True`, `glError=NoError`) — значит проблема вероятнее в шейдерной интерпретации/тонмаппинге/цветовом пайплайне, а не в «текстура не пришла».
4. Модель «белая даже в BaseColorOnly» — это сужает гипотезы до неправильного декодирования baseColor, перепутанных каналов/формата, слишком большого множителя в material factors/post-process, либо ошибки в debug-ветке `BaseColorOnly`.

---

## Цель

Сделать воспроизводимый и расширяемый пайплайн диагностики, чтобы:
- быстро разделять проблемы **данных модели** и **рендер-пайплайна**;
- получать однозначный ответ «где ломается цвет»;
- устранить текущий кейс и предотвратить регрессии.

---

## План работ

### Этап 1. Убрать ложные WARN и нормализовать preflight GLTF/GLB

1. Исправить `GltfDependencyInspector`:
   - определять тип файла по расширению + сигнатуре (`glTF` magic у GLB);
   - для `.gltf` читать JSON как сейчас;
   - для `.glb` читать JSON chunk из контейнера (или пропускать external URI scan с явным `Debug`-сообщением «embedded-only preflight»);
   - WARN переводить в «ожидаемое поведение» для GLB, если external URI не требуется.
2. Ввести единый `GltfPreflightResult` (данные + предупреждения + capability flags), чтобы расширять preflight без размазывания логики.

**Зачем:** чтобы не терять реальные ошибки цвета на фоне заведомо ложного warning-шума.

### Этап 2. Добавить «верификацию цветового пайплайна» в одном месте

1. Централизовать решения по цветовым пространствам (если ещё не централизовано):
   - `BaseColor` и `Emissive` всегда идут через sRGB decode;
   - `Normal`, `MetallicRoughness`, `Occlusion` — linear.
2. На этапе загрузки текстуры фиксировать:
   - исходный формат из glTF;
   - выбранный internal format GL;
   - активирован ли decode path (hardware sRGB или shader decode fallback);
   - итоговый `ColorPipelineStatus`.
3. Запретить «молчаливый fallback» для BaseColor/Emissive в linear без явного события диагностики уровня WARN/ERROR (настраиваемо).

**Зачем:** текущие `BaseColorMissingSrgbDecode` напрямую указывают на этот слой.

### Этап 3. Проверить ветки debug-режимов (особенно BaseColorOnly)

1. Ревью shader-веток режимов `BaseColorOnly`, `Unlit`, `PbrFull`:
   - одинаковая ли выборка baseColor texture;
   - не применяется ли лишний `pow/gamma/toneMapping/exposure` в debug-ветке;
   - одинаков ли clamp/scale путь.
2. Ввести «stage markers» в shader debug output:
   - `baseColorTexRaw`
   - `baseColorAfterSrgbDecode`
   - `baseColorAfterFactor`
   - `afterLighting`
   - `afterToneMapping`

**Зачем:** если белое уже в `baseColorAfterSrgbDecode`, значит проблема до света.

### Этап 4. Проверка material factors и расширений glTF

1. Логировать по материалу:
   - `baseColorFactor`, `emissiveFactor`, `metallicFactor`, `roughnessFactor`, `alphaMode`;
   - наличие `KHR_materials_emissive_strength` и реальное значение;
   - активные расширения (`specular`, `transmission`, `clearcoat` и т.д.).
2. Добавить защитные проверки:
   - если факторы выходят за ожидаемые диапазоны — отдельный WARN;
   - если emissive даёт неадекватный вклад — диагностический clamp в debug режиме.

**Зачем:** пересвет может приходить из факторов, даже при корректной текстуре.

### Этап 5. Свет/экспозиция/тонмаппинг: изоляция влияния

1. Добавить runtime-профили освещения:
   - `LightingOff` (только baseColor в linear pipeline),
   - `SingleWhiteLight`,
   - `IBLOnly`,
   - `FullScene`.
2. Добавить экспозиционный sweep (например, 0.25 / 0.5 / 1.0 / 2.0) и лог min/max luminance кадра.
3. Поддержать freeze тонмаппинга (напрямую в sRGB out) для сравнения.

**Зачем:** пользователь уже подозревает свет — нужен быстрый способ доказать/исключить.

### Этап 6. Golden-сцены и автопроверка без ручного кликанья

1. Зафиксировать 3 тестовые модели:
   - текущий `cylinder_sci_fi` (проблемный),
   - простая эталонная PBR-модель без emissive,
   - emissive-heavy модель.
2. Для каждой сцены сохранять снимки режимов (`Unlit`, `BaseColorOnly`, `Pbr`) + JSON-диагностику.
3. Добавить проверку в `tools/validate_pbr_snapshot.py`:
   - порог на долю «клиппинга в белый»;
   - порог на среднюю яркость;
   - проверка, что `BaseColorMissingSrgbDecode == false` для baseColor/emissive.

**Зачем:** исключить возврат бага в следующих итерациях.

### Этап 7. Рефакторинг под расширяемость

1. Вынести диагностику PBR в модуль вида `Rendering/Diagnostics/PbrColorPipelineDiagnostics`.
2. Вынести политику texture semantic → color space в отдельный резолвер (не «if-else» по проекту).
3. Стабилизировать структуру логов (ключ-значение) для машинного анализа.

**Зачем:** ожидаются частые правки; важно держать изменения локализованными.

---

## Какие логи собрать дополнительно (чек-лист)

### Обязательно

1. **Сводка по материалам модели** (все факторы и glTF-расширения по каждому material).
2. **Color pipeline diagnostics** по каждому semantic texture:
   - semantic, source format, GL internal format, sRGB decode mode, fallback reason.
3. **Uniform dump для кадра в проблемном режиме**:
   - exposure, tone mapping mode, debug mode, camera near/far.
4. **Luminance histogram / min-max** хотя бы по одному кадру в `BaseColorOnly` и `Pbr`.
5. **Сравнение одного и того же кадра** в режимах `Unlit`, `BaseColorOnly`, `Pbr`.

### Желательно

1. Информация о backend/драйвере OpenGL (версия, vendor, renderer).
2. Значение framebuffer color space (`GL_FRAMEBUFFER_SRGB` / equivalent state).
3. Режим HDR/LDR и формат цветового attachment.

---

## Быстрые гипотезы по вашему логу (приоритет)

1. **№1 (самая вероятная):** не выполняется sRGB decode для BaseColor/Emissive (`BaseColorMissingSrgbDecode`, `EmissiveMissingSrgbDecode`).
2. **№2:** debug-ветка `BaseColorOnly` не изолирована и всё ещё проходит через тонмаппинг/экспозицию/доп. множители.
3. **№3:** material factors (включая emissive strength) дают сильный пересвет.
4. **№4:** mismatch framebuffer/output gamma (двойной/нулевой gamma-correction).
5. **№5:** сам свет (интенсивности/IBL) усиливает проблему, но обычно не объясняет «белая даже в BaseColorOnly», поэтому ниже по приоритету.

---

## Критерии готовности фикса

- Проблемная модель корректно отображается минимум в режимах `Unlit`, `BaseColorOnly`, `Pbr`.
- В логах отсутствуют `BaseColorMissingSrgbDecode` и `EmissiveMissingSrgbDecode` для корректно загруженных текстур.
- GLTF/GLB preflight не выдаёт ложных WARN для валидного `.glb`.
- Есть регрессионная проверка (snapshot + thresholds), проходящая локально и в CI.
