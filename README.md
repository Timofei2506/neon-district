# NEON DISTRICT — Unity 6.3 LTS

Открытый город в духе классических городских боевиков. Десктоп (Windows x64), URP, Input System,
Cinemachine-совместимая камера, HDRP-уровень пост-обработки.

## Требования
- **Unity 6.3 LTS** (6000.3.x) — ставится через Unity Hub;
- ОС Windows 10/11 x64 (проект собирается и под Linux/macOS);
- ≥ 16 ГБ RAM, GPU с 6+ ГБ VRAM.

## Быстрый старт (5 минут)
1. Unity Hub → **Add project from disk** → папка `NeonDistrict`.
2. Открыть проект (первый импорт ассетов — 3–10 минут: город на 260k полигонов + текстуры).
3. Меню **Neon District → 1) Setup Project** (URP, тени, материалы Манхэттена).
4. Меню **Neon District → 2) Build Prefabs** (игрок, прохожий, машины).
5. Меню **Neon District → 4) Bake NavMesh** (один раз).
6. Меню **Neon District → 3) Build Playground Scene** → Play.

## Управление
| Действие | Клавиша |
|---|---|
| Движение | WASD |
| Бег | Shift |
| Прыжок | Space |
| Сесть/выйти из машины | E / F |
| Огонь | ЛКМ |
| Перезарядка | R |
| Руль | A / D |
| Ручник | Space |
| Пауза | Esc |

## Структура
```
Assets/
  Art/
    Manhattan/      — город (FBX + 106 текстур + layout.json + info.json)
    Characters/     — Renderpeople: игрок (walk), прохожий (idle)
    Vehicles/       — Mercedes GLS 580, Polestar 4 (с анимацией дверей)
    City/           — Seoul City Builder Kit (free sample, 40 моделей)
  Scripts/
    Core/           — GameManager, SaveSystem, ввод
    World/          — CityController, трафик, пешеходы, день/ночь
    Player/         — контроллер персонажа, оружие, машины
    Vehicles/       — физика машины
    Camera/         — орбитальная камера от третьего лица
    UI/             — HUD, меню (собираются кодом)
    Editor/         — сетап проекта, генерация префабов и сцены
```

## Ассеты и лицензии
- **Manhattan** — Sketchfab-модель (учебный/демо-проект).
- **Renderpeople** — Nathan (walk), Sophia (idle).
- **Seoul City Builder Kit (free sample)** — royalty-free, см. `Assets/Art/City/SEOUL_KIT_LICENSE.txt`.
- **Mercedes GLS 580**, **Mercedes AMG C63**, **Polestar 4**, **Corvette Stingray (NFS)** — из пользовательских наборов.

> ⚠️ Для публичного релиза замените модели с неясной лицензией (город, машины) или получите права.
> Код проекта — ваш, его можно лицензировать как угодно.
