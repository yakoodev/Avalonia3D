# PBR QA Checklist (регрессия baseColor/яркости)

Этот чеклист фиксирует единый сценарий QA для всех изменений рендера/материалов.

## Набор ассетов
Источник истины: `PBR_QA_ASSETS.json`.

Минимальный обязательный прогон:
- `car/scene.gltf` (problematic asset, white-wash контроль)
- `cylinder_sci_fi/scene.gltf` (problematic asset, потеря текстур/переэкспозиция)

Расширенный прогон:
- `doge/scene.gltf`

## Подготовка
1. Запустить `Avalonia3D.Sandbox`.
2. Выбрать сцену **PBR Regression (фикс. свет/камера)**.
3. Убедиться, что камера/свет не менялись вручную (фиксированная постановка).

## Быстрый ручной smoke (PBR/Unlit)
1. Переключить **PBR -> Unlit -> PBR**.
2. Проверить, что в PBR видны texture details baseColor (не плоский белый/серый).
3. Проверить, что в Unlit texture details также присутствуют.
4. Повторить переключение 3-5 раз, убедиться в стабильности.

## Snapshot-проверка (lightweight, non pixel-perfect)
Запускать скрипт:

```bash
python3 tools/validate_pbr_snapshot.py
```

Что проверяет:
- у материалов есть baseColor texture;
- средняя яркость baseColor текстур (`meanBrightness`) находится в диапазоне `[minMeanBrightness, maxMeanBrightness]`;
- доля near-white пикселей (`nearWhiteRatio`) не превышает `maxNearWhiteRatio`;
- дельта `PBR - Unlit` по средней яркости (`maxPbrToUnlitBrightnessDelta`) не превышает порог;
- доля «пересвеченных» пикселей в PBR-прокси (`maxPbrOverexposedRatio`) ниже порога;
- в отчёт печатается breakdown по материалам и семантикам текстур.

## Критерии приёмки
- Нет регрессии: baseColor-текстуры не "пропадают" в PBR.
- Нет выгорания: PBR не превращается в «белый силуэт», текстуры читаются на референсе.
- QA-скрипт падает при повторном появлении «белого налёта» или потере текстур.
- Ручное переключение PBR/Unlit стабильно и повторяемо.

## Как расширять
1. Добавить новый ассет в `PBR_QA_ASSETS.json`.
2. Прописать наблюдаемые артефакты в `knownArtifacts`.
3. Подобрать пороги яркости/near-white по эталонному прогону.
4. Обновить этот чеклист, если добавились новые обязательные шаги.
