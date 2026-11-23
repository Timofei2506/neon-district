using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonDistrict.World
{
    /// <summary>
    /// Данные города: районы, сетка улиц, точки интереса.
    /// Заполняется из manhattan_layout.json, а если его нет — из безопасных значений по умолчанию.
    /// </summary>
    [Serializable]
    public class CityLayout
    {
        [Serializable]
        public class ManhattanJson
        {
            public float angle_deg;
            public float[] size;
            public float[] bounds_min;
            public float[] bounds_max;
            public float[] streets_x;
            public float[] streets_z;
            public float built_ratio;
            public float cell;
        }

        public float angleDeg;
        public Vector3 size = new Vector3(8000f, 400f, 8000f);
        public List<float> streetsX = new List<float>();
        public List<float> streetsZ = new List<float>();
        public float cell = 8f;

        public static CityLayout FromJson(string json)
        {
            var layout = new CityLayout();
            try
            {
                var data = JsonUtility.FromJson<ManhattanJson>(json);
                if (data != null)
                {
                    layout.angleDeg = data.angle_deg;
                    layout.cell = data.cell > 0 ? data.cell : 8f;
                    if (data.size != null && data.size.Length >= 3)
                        layout.size = new Vector3(data.size[0], data.size[2], data.size[1]);
                    if (data.streets_x != null) layout.streetsX.AddRange(data.streets_x);
                    if (data.streets_z != null) layout.streetsZ.AddRange(data.streets_z);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CityLayout] " + e.Message);
            }
            if (layout.streetsX.Count == 0)
                for (int i = -25; i <= 25; i++) layout.streetsX.Add(i * 90f);
            if (layout.streetsZ.Count == 0)
                for (int i = -25; i <= 25; i++) layout.streetsZ.Add(i * 90f);
            return layout;
        }

        public Vector3 NearestIntersection(Vector3 p)
        {
            float bx = streetsX[0], bz = streetsZ[0], bdx = float.MaxValue, bdz = float.MaxValue;
            foreach (var x in streetsX) { float d = Mathf.Abs(p.x - x); if (d < bdx) { bdx = d; bx = x; } }
            foreach (var z in streetsZ) { float d = Mathf.Abs(p.z - z); if (d < bdz) { bdz = d; bz = z; } }
            return new Vector3(bx, p.y, bz);
        }
    }
}
