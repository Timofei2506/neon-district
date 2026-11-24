# NEON DISTRICT

Открытый город в духе классических городских боевиков — **Unity 6.3 LTS**, десктоп (Windows x64), URP.
Город — реальная детальная модель Манхэттена (260 000 полигонов, 8 × 8 км), плюс Renderpeople-персонажи,
три машины (Mercedes GLS 580, Polestar 4 с анимацией дверей, Corvette Stingray) и 40 городских ассетов Seoul City Builder Kit.

![Key art](docs/key_art.png)

## Что уже есть (v0.1, вертикальный срез)

| Система | Состояние |
|---|---|
| Город Манхэттен: геометрия, 106 текстур, материалы | ✅ импортируется автоматически |
| Сетка улиц (анализ модели → JSON) для трафика и пешеходов | ✅ |
| Игрок: CharacterController, бег/прыжок/присед, Humanoid-анимация | ✅ |
| Процедурный «умный» анимационный слой: наклон корпуса, отдача, приземление | ✅ |
| Камера от третьего лица с коллизией и режимом машины | ✅ |
| Машины: физика, крен/клевок, фары, стопы, ручник, звук-RPM хук | ✅ |
| Трафик: 16 машин, повороты на перекрёстках, дистанция, торможение перед игроком | ✅ |
| Пешеходы: NavMesh, блуждание, испуг от выстрела, падение | ✅ |
| Цикл суток URP: солнце/луна, туман, неон, bloom/vignette, экспозиция | ✅ |
| HUD: часы, район, деньги, розыск, здоровье/броня, спидометр, патроны | ✅ |
| Меню, пауза, сохранение/загрузка (JSON) | ✅ |
| Стрельба: raycast, урон, гильзы/вспышка (заготовка) | ✅ |
| Пост-обработка: ACES, bloom, виньетка, цветокоррекция, SSAO | ✅ |
| **Полиция**: 3 патруля (GLS / AMG / Polestar) с ливреей, мигалками, синтезированной сиреной и ИИ погони | ✅ |
| **GitHub Actions**: автосборка Windows + Linux, артефакты и релизы по тегу | ✅ |
| **Миссии**: 6 сюжетных заказов, 5 типов шагов, маркер в мире, таймеры, гонорары | ✅ |
| **Диалоги**: субтитры с печатью текста, letterbox, реплики «по рации», приглушение звука | ✅ |
| **Миникарта**: ортокамера → RenderTexture, круглая маска, поворот по игроку, метки цели и патрулей | ✅ |
| **Гараж и экономика**: 4 машины, покупка/продажа (60 %), выбор, вызов механиком | ✅ |
| **Погода**: яс/облачно/дождь/гроза, дождь в кадре камеры, мокрый асфальт, вспышки молний | ✅ |
| **Пауза**: продолжение, гараж, сохранение/загрузка, выход в меню | ✅ |
| **Проверка компиляции без Unity**: Roslyn + reference-сборки Unity в CI (`tools/cscheck`) | ✅ |

Дальше по плану: Motion Matching для персонажа, лицензионный саундтрек и радио-станции, LOD/оптимизация до стабильных 60 fps, интерьеры, полицейские миссии-облавы.

## Требования
- **Unity 6.3 LTS** (6000.3.x) — ставится через Unity Hub;
- Windows 10/11 x64 (проект собирается и под Linux/macOS);
- 16 ГБ RAM, GPU 6+ ГБ VRAM (для 1080p high).

## Быстрый старт (5 минут)
1. Unity Hub → **Add project from disk** → папка `NeonDistrict`.
2. Открыть проект. Первый импорт — 3–10 минут (город + текстуры).
3. Меню **Neon District → 1) Setup Project** — URP, тени, материалы Манхэттена, Player Settings.
4. Меню **Neon District → 2) Build Prefabs** — игрок, прохожий, три машины, аниматоры.
5. Меню **Neon District → 4) Bake NavMesh** — один раз (1–3 минуты).
6. Меню **Neon District → 3) Build Playground Scene** → **Play**.

Если сцена не собралась — откройте `Assets/Scenes/Playground.unity` вручную, она создаётся генератором.

## Миссии, диалоги, миникарта
| Файл | Что делает |
|---|---|
| `Missions/MissionCatalog.cs` | 6 миссий (Маркус / Лин), шаги: доехать, взять машину, довезти, выдержать розыск, чекпоинты |
| `Missions/MissionSystem.cs` | машина состояний шагов, процедурные цели на сетке улиц, пульсирующий маркер в мире, гонорары |
| `Missions/CinematicDialogue.cs` | субтитры с посимвольной печатью, letterbox, приглушение звука, пропуск по Space/Enter |
| `Missions/MissionHudBinder.cs` | связывает миссии ↔ субтитры ↔ миникарту ↔ HUD; `J` — взять следующий заказ |
| `UI/Minimap.cs`, `UI/MinimapView.cs` | ортокамера сверху → RenderTexture → круглая UGUI-карта с метками |
| `UI/HudMissionPanel.cs` | панель цели, таймер, счётчик шагов, всплывающие карточки |

Цели миссий выбираются **процедурно** (`CityController.RandomSpawnPoint`) — на сетке улиц Манхэттена,
поэтому миссии не привязаны к конкретным координатам и переживают правки карты.

## Гараж и экономика
`Economy/GarageSystem.cs` + `UI/GarageUI.cs`: четыре машины (AMG C63 — «твоя» с самого начала),
цены $4 200 … $18 000, продажа за 60 % стоимости, вызов выбранной машины к игроку за $60.
Владение и выбор хранятся в `PlayerPrefs`, деньги — в `GameManager` и JSON-сейве. Клавиша `G`.

## Погода
`World/WeatherSystem.cs`: ясно → облачно → дождь → гроза по таймеру (25–70 игровых минут).
Дождь — GPU-партиклы, висящие над **камерой** (капли всегда в кадре и не «летят» за игроком),
брызги на земле, повышенная плотность тумана, вспышки молний, «мокрый» асфальт через
`_Smoothness` материалов города. День/ночь по-прежнему ведёт `DayNightCycle` — погода только ослабляет солнце.

## Полиция и розыск
Всё в `Assets/Scripts/Police`:

| Файл | Что делает |
|---|---|
| `PoliceLivery.cs` | превращает любую машину в патруль: белый кузов, ливрея на бортах (декали), LED-балка, отбойник, антенна. Мигалки — красно/синяя последовательность как у настоящей полиции |
| `PoliceAI.cs` | догоняет игрока «наперерез», таранит, прижимает; при остановке рядом — задержание со штрафом `$250 × уровень` |
| `PoliceSpawner.cs` | уровень розыска 1 → 1 патруль, 3 → 2, 5 → 3; спавн за спиной, чтобы не появлялись в кадре |
| `PoliceSiren.cs` | двухтональная сирена, **синтезируется в рантайме** (не нужны аудиофайлы), 3D-затухание |

Патрули собираются из уже имеющихся машин: `PoliceCar` (Mercedes GLS), `Police_AMG` (C63), `Police_Polestar`.
Ливрея и эмиссия мигалок генерируются скриптом `Assets/Art/Vehicles/Textures/make_police_textures.py`.

## CI/CD (GitHub Actions)
| Workflow | Что делает |
|---|---|
| `.github/workflows/verify.yml` | 1–2 мин: структура проекта, JSON/`.inputactions`, санитайзинг C# (`tools/check_csharp.py`), **настоящая компиляция** Roslyn'ом против reference-сборок Unity (`tools/cscheck`) |
| `.github/workflows/build.yml` | сборка **Windows x64** и **Linux x64** через `game-ci/unity-builder`, артефакты 30 дней, релиз при пуше тега `v*` |

Сборка вызывает `NeonDistrict.EditorTools.CIBuild.BuildWindows` — метод сам готовит проект (URP, префабы, сцена) и собирает билд.
Путь сборки берётся из `-customBuildPath`, который передаёт game-ci, поэтому артефакты всегда попадают в ожидаемую папку.

### Проверка компиляции без Unity Editor
`tools/cscheck/run.sh` скачивает .NET SDK и reference-сборки UnityEngine/UnityEditor из NuGet и компилирует
**все** скрипты проекта Roslyn'ом в двух конфигурациях (`legacy` и `UNITY_6000_0_OR_NEWER`).
Это ловит 90 % ошибок «метод переименован / забыт using / поле не найдено» за ~10 секунд вместо 30 минут сборки:

```bash
bash tools/cscheck/run.sh      # локально; в CI то же самое делает verify.yml
```

### Статус CI
`Build (Windows / Linux)` — **зелёный**: обе платформы собираются, по тегу создаётся GitHub Release
с готовыми архивами. Последний релиз: **[v0.1.0](https://github.com/Timofei2506/neon-district/releases/tag/v0.1.0)**
(Windows 90 МБ / Linux 61 МБ). Артефакты каждого прогона дополнительно живут 30 дней.

### Активация Unity в CI (Unity Personal, файл лицензии не нужен)
С Unity 6 **ручная активация Personal закрыта** (`license.unity3d.com/manual` обслуживает только
Enterprise/Industry), а `.ulf` из Unity Hub **привязан к машине** — в облаке он даёт
`Code 400: Machine bindings don't match`. Поэтому лицензия берётся **по аккаунту**:

| Лицензия | Секреты |
|---|---|
| Unity Personal (бесплатно) | `UNITY_EMAIL`, `UNITY_PASSWORD` |
| Unity Pro / Plus | `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD` |

Секреты: https://github.com/Timofei2506/neon-district/settings/secrets/actions

Сборка идёт через CLI, а не `unity-builder@v4` — тот в принципе не умеет Personal
(в его коде без `.ulf`/серийника бросается `Missing Unity License File and no Serial`):

```bash
game-ci activate --engine unity --unityLicensingMethod personal \
  --customImage unityci/editor:ubuntu-6000.3.29f1-base-4.0.0
game-ci build --engine unity --targetPlatform StandaloneWindows64 ... --skipActivation \
  --customImage unityci/editor:ubuntu-6000.3.29f1-windows-mono-4.0.0
game-ci return-license --engine unity --unityLicensingMethod personal   # шаг if: always()
```

Что важно знать:
- каждый прогон занимает **одно место** (seat) на аккаунте, шаг *Return Unity license* возвращает его;
  Windows- и Linux-джобы идут последовательно, параллельные прогоны запускать не стоит;
- если место «залипло» — откройте Unity Hub локально и выйдите из аккаунта (на Personal выход = возврат);
- аккаунт с 2FA в CI будет требовать код — лучше отдельный CI-аккаунт без 2FA;
- образы фиксированы явно: `linux-il2cpp` **не содержит** Windows-модуль, для `StandaloneWindows64`
  нужен `windows-mono`, для активации достаточно `base`;
- `CIBuild` печатает блок `# Build results #` с `Errors:`/`Size:` — именно его парсит game-ci,
  без него успешная сборка помечается как «There was an error building the project».

## Как поиграть

### Скачать готовый билд (ничего устанавливать не нужно)
1. Откройте **[Releases](https://github.com/Timofei2506/neon-district/releases/latest)** и скачайте:
   - **Windows**: `NeonDistrict-Windows.zip` (≈90 МБ)
   - **Linux**: `NeonDistrict-Linux.zip` (≈61 МБ)
2. Распакуйте архив целиком (не запускайте изнутри zip-просмотрщика — рядом должны лежать
   `StandaloneWindows64_Data/` и `UnityPlayer.dll`).
3. Запустите `StandaloneWindows64.exe` (Linux: `chmod +x StandaloneLinux64.x86_64` и запуск оттуда же).

Требования: Windows 10/11 x64 или Linux x64 с драйвером D3D12/Vulkan; видеопамять от 2 ГБ.
Первый запуск 10–20 секунд — грузится город (один меш Манхэттена, ~260 тыс. трисов).

> Антивирус/SmartScreen может ругаться на неподписанный exe — «Подробнее» → «Выполнить в любом случае».

### Играть в редакторе Unity
1. Unity Hub → **Add project from disk** → папка репозитория, откройте в **Unity 6000.3.29f1**.
2. Меню **Neon District → 1) Setup Project**, затем **2) Build Prefabs**, **3) Build Playground Scene**.
3. Откройте `Assets/Scenes/Playground.unity` и нажмите **Play**.
   (CI делает то же самое автоматически: `CIBuild.PrepareProject()` → `BuildPipeline.BuildPlayer`.)

### Что делать в игре
- Стартуете в 08:30 с $500; в HUD — часы, деньги, розыск, миникарта.
- **J** — взять заказ у диспетчера (миссии `m01`…`m06`, от $600 до $6000, есть таймеры на звёзды).
- **G** — гараж: AMG бесплатно, GLS $4200, Corvette $12500, Polestar $18000 (продажа за 60 %).
- **E/F** — сесть в машину и выйти; **Space** — ручник; за превышение скорости и стрельбу растёт
  розыск, полиция начинает преследовать, штраф $250 × уровень.
- **Esc** — пауза, **M** — карта, **Tab** — журнал миссий, погода меняется сама (ясно → пасмурно → дождь → гроза).

## Управление
| Действие | Клавиша |
|---|---|
| Движение | W A S D |
| Бег | Shift |
| Прыжок / ручник | Space |
| Сесть / выйти из машины | E или F |
| Огонь | ЛКМ |
| Перезарядка | R |
| Присед | C |
| Взять заказ (миссия) | J |
| Гараж | G |
| Пропустить реплику | Space / Enter |
| Пауза | Esc |

## Структура проекта
```
Assets/
  Art/
    Manhattan/        город: Manhattan.fbx + Textures/ (106) + manhattan_layout.json + manhattan_info.json
    Characters/       Player_Male.fbx (walk), NPC_Female.fbx (idle) + текстуры
    Vehicles/         Mercedes_GLS.fbx, Polestar_4.fbx (двери, анимации), Corvette_Stingray.fbx
    City/             Seoul City Builder Kit (free sample): здания, деревья, реквизит, знаки, седаны
    Animation/        сгенерированные Animator Controller'ы
    Materials/        URP-материалы персонажей и машин
  Scripts/
    Core/             GameManager, SaveSystem, InputActions, InputBridge
    World/            CityController, CityLayout, TrafficSystem, PedestrianSpawner, DayNightCycle, WeatherSystem
    Player/           PlayerController, LocomotionAnimator, CharacterNormalizer
    Entities/         Pedestrian, CharacterNormalizer
    Vehicles/         VehicleController (аркадная физика + свет + звук-RPM)
    Police/           PoliceLivery, PoliceAI, PoliceSpawner, PoliceSiren
    Missions/         MissionCatalog, MissionSystem, CinematicDialogue, MissionHudBinder
    Economy/          GarageSystem (каталог машин, покупка/продажа/вызов)
    Camera/           ThirdPersonOrbit
    UI/               HUDController, RuntimeUI, Minimap + MinimapView, GarageUI, PauseMenuUI, HudMissionPanel
    Editor/           ProjectSetup, AssetPipeline, SceneSetup, CIBuild (меню Neon District)
  Scenes/             Playground.unity (генерируется)
docs/                 key art + Blender-превью ассетов
tools/                Blender-пайплайн конвертации + cscheck (компиляция C# без Unity)
```

## Инструменты ассетов (Blender, вне Unity)
Всё, что нужно для повторной подготовки моделей, лежит в `tools/`:
- `export_unity_fbx.py` — FBX/GLB → Unity-FBX: метры, Y-up, −Z вперёд, пивот в центре днища, выравнивание длинной оси по X.
- `analyze_city.py` — анализ городской модели: угол сетки, границы, карта занятости, линии улиц → JSON.
- `export_manhattan.py` — разворот города под «ровную» сетку и экспорт.
- `export_asset.py` — GLB-пайплайн (текстуры, LOD, анимации) для веб-прототипов.

Запуск: `blender -b --factory-startup --python tools/export_unity_fbx.py -- <src> <dst> car 5.2`

## Лицензии и происхождение ассетов
- **Manhattan** — Sketchfab-модель (демо/учебный проект). **Mercedes GLS 580 / AMG C63**, **Corvette Stingray (NFS Unbound)**, **Polestar 4** — пользовательские наборы.
- **Renderpeople** — Nathan (walk), Sophia (idle).
- **Seoul City Builder Kit (free sample)** — royalty-free, текст: `Assets/Art/City/SEOUL_KIT_LICENSE.txt`.

> ⚠️ Для публичного релиза нужно подтвердить права на город и машины либо заменить их. Код игры — ваш.

## Производительность
- Город статичен → static batching, тени 4096, 4 каскада.
- Модели машин: Polestar 616k трис — для трафика используйте LOD (генератор `NeonDistrict → 2)`) или замените трафик на `Assets/Art/City/vehicles/vehicle_sedan` (4.3k трис).
- Пешеходы — до 26 одновременно, спавн/деспавн по радиусу.
