#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NeonDistrict.EditorTools
{
    /// <summary>
    /// Режет один большой меш города на части по сетке — иначе фрустум-каллинг
    /// бесполезен: пока в кадре хотя бы один треугольник меша, GPU рисует весь меш.
    ///
    /// Работает на уже импортированной модели (внутри ассета), поэтому результат
    /// попадает в сам FBX-ассет и не требует отдельных .asset-файлов на тысячи чанков.
    ///
    /// Алгоритм:
    ///  1. треугольники раскладываются по «ячейкам» cellSize × cellSize (по XZ);
    ///  2. для каждой непустой ячейки собирается под-меш (позиции/нормали/UV/индексы);
    ///  3. под-меши добавляются в ассет как объекты и вешаются на дочерние объекты.
    /// </summary>
    public static class MeshChunker
    {
        public struct Result
        {
            public int chunks;
            public int triangles;
        }

        /// <summary>Разбить меш-объект на чанки. Возвращает число созданных чанков.</summary>
        public static int Split(GameObject source, float cellSize)
        {
            var mf = source.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return 0;
            var mesh = mf.sharedMesh;

            var verts = mesh.vertices;
            var normals = mesh.normals;
            var uvs = mesh.uv;
            var uv2 = mesh.uv2;
            var colors = mesh.colors;
            var tris = mesh.triangles;
            if (verts.Length == 0 || tris.Length < 3) return 0;

            // 1) раскладываем треугольники по ячейкам
            var buckets = new Dictionary<long, List<int>>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 c = (verts[tris[t]] + verts[tris[t + 1]] + verts[tris[t + 2]]) / 3f;
                long key = CellKey(c, cellSize);
                if (!buckets.TryGetValue(key, out var list))
                {
                    list = new List<int>(tris.Length / 8 + 3);
                    buckets[key] = list;
                }
                list.Add(tris[t]); list.Add(tris[t + 1]); list.Add(tris[t + 2]);
            }

            if (buckets.Count < 2) return 0;   // резать нечего

            var parent = source.transform.parent != null ? source.transform.parent : source.transform;
            int made = 0;

            foreach (var kv in buckets)
            {
                var list = kv.Value;
                if (list.Count < 3) continue;

                // 2) ремап индексов: каждый треугольник получает свои вершины
                //    (дубли вершин допустимы: это дешевле, чем искать общие)
                var remap = new Dictionary<int, int>(list.Count);
                var nv = new List<Vector3>(list.Count);
                var nn = new List<Vector3>(list.Count);
                var nu = new List<Vector2>(list.Count);
                var nu2 = new List<Vector2>(list.Count);
                var nc = new List<Color>(list.Count);
                var nt = new List<int>(list.Count);

                foreach (int vi in list)
                {
                    if (!remap.TryGetValue(vi, out int ni))
                    {
                        ni = nv.Count;
                        remap[vi] = ni;
                        nv.Add(verts[vi]);
                        if (normals.Length > vi) nn.Add(normals[vi]);
                        if (uvs.Length > vi) nu.Add(uvs[vi]);
                        if (uv2.Length > vi) nu2.Add(uv2[vi]);
                        if (colors.Length > vi) nc.Add(colors[vi]);
                    }
                    nt.Add(ni);
                }

                var sub = new Mesh();
                sub.name = mesh.name + "_chunk" + made;
                sub.indexFormat = nv.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
                sub.SetVertices(nv);
                if (nn.Count == nv.Count) sub.SetNormals(nn);
                if (nu.Count == nv.Count) sub.SetUVs(0, nu);
                if (nu2.Count == nv.Count) sub.SetUVs(1, nu2);
                if (nc.Count == nv.Count) sub.SetColors(nc);
                sub.SetTriangles(nt, 0, true);

                // добавляем в тот же ассет, что и оригинал
                string assetPath = AssetDatabase.GetAssetPath(mesh);
                AssetDatabase.AddObjectToAsset(sub, assetPath);

                var go = new GameObject(source.name + "_c" + made);
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                go.transform.localScale = source.transform.localScale;
                go.layer = source.layer;
                go.isStatic = true;

                var nmf = go.AddComponent<MeshFilter>();
                nmf.sharedMesh = sub;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterials = (Material[])source.GetComponent<Renderer>().sharedMaterials.Clone();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                mr.receiveShadows = true;

                made++;
            }

            // оригинал выключаем (а не удаляем: он нужен как «источник» при повторном запуске)
            source.SetActive(false);
            AssetDatabase.SaveAssets();
            return made;
        }

        private static long CellKey(Vector3 p, float cell)
        {
            long cx = (long)Mathf.Floor(p.x / cell);
            long cz = (long)Mathf.Floor(p.z / cell);
            return (cx << 32) ^ (cz & 0xFFFFFFFFL);
        }
    }
}
#endif
