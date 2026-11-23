using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using NeonDistrict.Player;

namespace NeonDistrict.Core
{
    /// <summary>
    /// Сохранение прогресса в JSON (Application.persistentDataPath).
    /// Пока храним только «что нужно для проверки вертикального среза».
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public float money;
        public float timeOfDay;
        public int day;
        public float playerHealth;
        public Vector3 playerPosition;
        public float playerRotationY;
        public int ammoPistol;
        public int wanted;
        public string scene;
        public string[] flags = Array.Empty<string>();
    }

    public static class SaveSystem
    {
        private static string Path => System.IO.Path.Combine(Application.persistentDataPath, "neon_district_save.json");

        public static bool HasSave => System.IO.File.Exists(Path);

        public static void Save(GameManager gm, PlayerController player)
        {
            var data = new SaveData
            {
                money = gm.Money,
                timeOfDay = gm.TimeOfDay,
                day = gm.Day,
                playerHealth = player != null ? player.Health : 100f,
                playerPosition = player != null ? player.transform.position : Vector3.zero,
                playerRotationY = player != null ? player.transform.eulerAngles.y : 0f,
                ammoPistol = player != null ? player.AmmoPistol : 0,
                wanted = gm.Wanted,
                scene = SceneManager.GetActiveScene().name,
            };
            System.IO.File.WriteAllText(Path, JsonUtility.ToJson(data, true));
            Debug.Log($"[Save] -> {Path}");
        }

        public static void LoadInto(GameManager gm, PlayerController player)
        {
            if (!HasSave) return;
            try
            {
                var data = JsonUtility.FromJson<SaveData>(System.IO.File.ReadAllText(Path));
                if (data == null) return;
                gm.AddMoney(data.money - gm.Money);
                if (player != null)
                {
                    player.transform.position = data.playerPosition;
                    player.transform.rotation = Quaternion.Euler(0f, data.playerRotationY, 0f);
                    player.Health = data.playerHealth;
                    player.AmmoPistol = data.ammoPistol;
                }
                Debug.Log("[Save] loaded");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] failed: {e.Message}");
            }
        }

        public static void Delete()
        {
            if (HasSave) System.IO.File.Delete(Path);
        }
    }
}
