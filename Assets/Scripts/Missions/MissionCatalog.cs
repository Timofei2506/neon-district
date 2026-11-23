using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonDistrict.Missions
{
    /// <summary>Тип шага миссии. Определяет, что именно проверяет MissionSystem.</summary>
    public enum StepKind
    {
        /// <summary>Дойти/доехать до точки (пешком или на машине).</summary>
        GoTo = 0,
        /// <summary>Сесть в машину, стоящую в точке (нужен любой автомобиль рядом).</summary>
        TakeVehicle = 1,
        /// <summary>Довезти машину до точки и остановиться.</summary>
        Deliver = 2,
        /// <summary>Продержаться заданное время с нужным уровнем розыска.</summary>
        Survive = 3,
        /// <summary>Проехать серию чекпоинтов подряд (каждый — отдельная точка).</summary>
        Checkpoint = 4,
    }

    /// <summary>Один шаг миссии: цель, условия и реплика по радио при старте шага.</summary>
    [Serializable]
    public class MissionStep
    {
        public StepKind kind = StepKind.GoTo;
        [Tooltip("Текст цели в HUD")] public string objective = "";
        [Tooltip("Реплика контакта по рации при старте шага")] public string radioLine = "";
        [Tooltip("Кто говорит (имя в субтитрах)")] public string speaker = "ДИСПЕТЧЕР";

        [Tooltip("Точка цели в мире (если resolveOnRoad = false)")]
        public Vector3 point;
        [Tooltip("Выбрать точку процедурно на ближайшей дороге от игрока")]
        public bool resolveOnRoad = false;
        [Tooltip("На каком расстоянии от игрока искать точку (м)")]
        public float spawnDistance = 420f;

        [Tooltip("Радиус срабатывания, м")] public float radius = 14f;
        [Tooltip("Лимит времени на шаг, с (0 — без лимита)")] public float timeLimit = 0f;
        [Tooltip("Сколько секунд нужно выдержать (для Survive/Checkpoint-серий)")] public float duration = 0f;
        [Tooltip("Требуемый уровень розыска (для Survive)")] public int wanted = 0;
        [Tooltip("Максимальная скорость для засчитывания доставки, км/ч")] public float deliverSpeedKmh = 8f;

        public static MissionStep Go(string objective, string radioLine, float spawnDistance = 420f, float radius = 16f, float timeLimit = 0f)
        {
            return new MissionStep
            {
                kind = StepKind.GoTo, objective = objective, radioLine = radioLine,
                resolveOnRoad = true, spawnDistance = spawnDistance, radius = radius, timeLimit = timeLimit,
            };
        }

        public static MissionStep Take(string objective, string radioLine, float radius = 18f)
        {
            return new MissionStep
            {
                kind = StepKind.TakeVehicle, objective = objective, radioLine = radioLine,
                resolveOnRoad = true, spawnDistance = 140f, radius = radius,
            };
        }

        public static MissionStep DeliverTo(string objective, string radioLine, float spawnDistance = 900f, float radius = 20f, float timeLimit = 0f)
        {
            return new MissionStep
            {
                kind = StepKind.Deliver, objective = objective, radioLine = radioLine,
                resolveOnRoad = true, spawnDistance = spawnDistance, radius = radius, timeLimit = timeLimit,
            };
        }

        public static MissionStep SurviveWith(int stars, float seconds, string objective, string radioLine)
        {
            return new MissionStep
            {
                kind = StepKind.Survive, objective = objective, radioLine = radioLine,
                wanted = Mathf.Clamp(stars, 1, 5), duration = seconds,
            };
        }
    }

    /// <summary>Описание миссии: контакт, бриф, шаги и награда.</summary>
    [Serializable]
    public class MissionDefinition
    {
        public string id = "mission";
        public string title = "Миссия";
        public string contact = "МАРКУС";
        public string brief = "";
        public int reward = 1000;
        public bool clearWantedOnComplete = true;
        public MissionStep[] steps = Array.Empty<MissionStep>();
    }

    /// <summary>
    /// Каталог миссий. Пока живёт в коде (легко править и ревьюить в git),
    /// позже переедет в ScriptableObject-ассеты без изменения API MissionSystem.
    ///
    /// География: точки выбираются процедурно на сетке улиц Манхэттена
    /// (CityController.RandomSpawnPoint), поэтому миссии не привязаны
    /// к конкретным координатам и работают на любой карте с той же сеткой.
    /// </summary>
    public static class MissionCatalog
    {
        public static List<MissionDefinition> Build()
        {
            var list = new List<MissionDefinition>();

            list.Add(new MissionDefinition
            {
                id = "m01_wake_up",
                title = "Пробуждение",
                contact = "МАРКУС",
                reward = 600,
                brief = "Маркус: «Ты в городе неделю и уже должен мне. Начнём с простого: доедь до точки, посмотри, кому мы тут нужны.»",
                steps = new[]
                {
                    MissionStep.Go("Доберись до перекрёстка в Хвандаме", "Встречаемся на углу. Не опаздывай — я не люблю ждать.", 320f, 18f, 180f),
                },
            });

            list.Add(new MissionDefinition
            {
                id = "m02_courier",
                title = "Курьер",
                contact = "МАРКУС",
                reward = 1200,
                brief = "Маркус: «Возьми тачку у обочины и довези её целой. Кузов помнёшь — вычту из твоих.»",
                steps = new[]
                {
                    MissionStep.Take("Сядь в машину у перекрёстка", "Машина твоя, ключи в замке. Только не разбей — я знаю, где ты живёшь."),
                    MissionStep.DeliverTo("Довези машину в порт (остановись в круге)", "Порт, южный причал. Не гони: мне нужен кузов, а не жестянка.", 900f, 22f, 240f),
                },
            });

            list.Add(new MissionDefinition
            {
                id = "m03_shadow_run",
                title = "Гонка с тенью",
                contact = "ЛИН",
                reward = 1500,
                brief = "Лин: «Хочешь работу? Проверим, как ты держишь машину. Три точки, одна попытка.»",
                steps = new[]
                {
                    MissionStep.Go("Старт у неонового перекрёстка", "Три чекпоинта. Время пошло, как только ты тронешься.", 260f, 16f),
                    MissionStep.DeliverTo("Чекпоинт 1", "Дальше — на север, между высотками.", 700f, 20f, 90f),
                    MissionStep.DeliverTo("Чекпоинт 2", "Хорошо идёшь. Второй — у эстакады.", 700f, 20f, 80f),
                    MissionStep.DeliverTo("Финиш", "Последний рывок. Не сбей зеркала — они дороже тебя.", 700f, 22f, 90f),
                },
            });

            list.Add(new MissionDefinition
            {
                id = "m04_hot_cargo",
                title = "Горячий груз",
                contact = "ЛИН",
                reward = 2200,
                brief = "Лин: «Груз в багажнике, и по нему уже плачут копы. Довози и не останавливайся.»",
                steps = new[]
                {
                    MissionStep.Take("Забери машину с грузом", "Машина на углу. В бардачке — то, из-за чего нас будут искать."),
                    MissionStep.SurviveWith(2, 40f, "Уйди от погони (40 секунд)", "Копы уже в эфире. Держись, я сбиваю их с маршрута."),
                    MissionStep.DeliverTo("Довези груз в гараж", "Всё чисто. Гараж в двух кварталах — завози и уходи.", 600f, 22f, 180f),
                },
            });

            list.Add(new MissionDefinition
            {
                id = "m05_quiet_deal",
                title = "Тихая сделка",
                contact = "МАРКУС",
                reward = 3000,
                brief = "Маркус: «Сделка на нейтральной территории. Если начнётся шум — уходи на своих двоих.»",
                steps = new[]
                {
                    MissionStep.Go("Приезжай на встречу у порта", "Место чистое. Пока чистое.", 500f, 18f, 200f),
                    MissionStep.SurviveWith(3, 60f, "Выдержи облаву (60 секунд)", "Это подстава! Держись минуту, я подниму шум в эфире."),
                    MissionStep.Go("Уйди в переулки", "Всё, уходи. Встретимся, когда стихнет.", 400f, 20f),
                },
            });

            list.Add(new MissionDefinition
            {
                id = "m06_neon_dawn",
                title = "Неоновый рассвет",
                contact = "ЛИН",
                reward = 6000,
                brief = "Лин: «Последнее дело. Четыре точки по всему острову, и город твой. Не подведи.»",
                steps = new[]
                {
                    MissionStep.Go("Старт: нижний Манхэттен", "Это последний заезд. Я верю в тебя, но проверю.", 300f, 18f),
                    MissionStep.DeliverTo("Точка 1 — Хвандам", "Первый. Дальше на восток.", 800f, 20f, 110f),
                    MissionStep.DeliverTo("Точка 2 — Сомун", "Второй. Копы нервничают — не дай им повода.", 800f, 20f, 100f),
                    MissionStep.DeliverTo("Точка 3 — Куро", "Третий. Половина города уже знает, кто ты.", 800f, 20f, 100f),
                    MissionStep.SurviveWith(4, 45f, "Уйди от погони (45 секунд)", "Они перекрыли мост. Держись!"),
                    MissionStep.DeliverTo("Финиш — порт Сонсан", "Последняя прямая. Не сбавляй.", 700f, 24f, 120f),
                },
            });

            return list;
        }
    }
}
