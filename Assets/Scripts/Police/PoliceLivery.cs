using System.Collections.Generic;
using UnityEngine;
using NeonDistrict.Vehicles;

namespace NeonDistrict.Police
{
    /// <summary>
    /// Превращает любую машину в полицейскую: белый кузов, служебная ливрея на дверях,
    /// светодиодная балка на крыше (красный/синий), отбойник, проблесковый фонарь и антенна.
    ///
    /// Всё строится процедурно, поэтому не зависит от UV конкретной модели:
    /// ливрея — отдельные плоскости-декали на бортах, они читаемы с любого ракурса.
    /// </summary>
    [ExecuteAlways]
    public class PoliceLivery : MonoBehaviour
    {
        [Header("Материалы")]
        public Texture2D liveryTexture;
        public Texture2D lightBarEmission;
        public Material bodyWhiteMaterial;

        [Header("Геометрия")]
        public Vector3 lightBarOffset = new Vector3(0f, 1.92f, 0.15f);
        public Vector3 lightBarSize = new Vector3(1.35f, 0.14f, 0.34f);
        public Vector2 decalSize = new Vector2(2.6f, 1.0f);
        public float decalHeight = 0.95f;
        public float decalOffsetFromCenter = 1.09f;

        [Header("Свет")]
        public float flashInterval = 0.12f;
        public float lightIntensity = 9f;
        public bool sirensOn = true;

        private readonly List<Light> _red = new List<Light>();
        private readonly List<Light> _blue = new List<Light>();
        private readonly List<Renderer> _redRenderers = new List<Renderer>();
        private readonly List<Renderer> _blueRenderers = new List<Renderer>();
        private float _timer;
        private int _phase;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private void Start()
        {
            if (transform.childCount == 0 || !transform.Find("PoliceKit")) Build();
            SetSirens(sirensOn);
        }

        [ContextMenu("Собрать полицейский комплект")]
        public void Build()
        {
            var kit = new GameObject("PoliceKit");
            kit.transform.SetParent(transform, false);

            PaintBody();
            BuildLightBar(kit.transform);
            BuildDecals(kit.transform);
            BuildPushBar(kit.transform);
            BuildAntenna(kit.transform);
        }

        // ---------------------------------------------------------------- body
        private void PaintBody()
        {
            var white = bodyWhiteMaterial != null ? bodyWhiteMaterial : MakeWhitePaint();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(transform.Find("PoliceKit") ?? transform) && r.name.Contains("Decal")) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;
                var swapped = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) { swapped[i] = m; continue; }
                    var n = m.name.ToLowerInvariant();
                    // кузовные панели красим, стёкла/шины/хром не трогаем
                    bool isBody = n.Contains("polar_white") || n.Contains("color_") || n.Contains("paint") || n.Contains("body") || n.Contains("material");
                    swapped[i] = isBody ? white : m;
                }
                r.sharedMaterials = swapped;
            }
        }

        private static Material MakeWhitePaint()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "Police_WhitePaint";
            m.color = new Color(0.94f, 0.95f, 0.96f);
            m.SetFloat("_Metallic", 0.25f);
            m.SetFloat("_Smoothness", 0.85f);
            return m;
        }

        // ---------------------------------------------------------------- light bar
        private void BuildLightBar(Transform parent)
        {
            var barRoot = new GameObject("LightBar");
            barRoot.transform.SetParent(parent, false);
            barRoot.transform.localPosition = lightBarOffset;

            // основание балки
            var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseGo.name = "LightBarBase";
            DestroyCollider(baseGo);
            baseGo.transform.SetParent(barRoot.transform, false);
            baseGo.transform.localScale = lightBarSize;
            baseGo.GetComponent<Renderer>().sharedMaterial = MakeBlackPlastic();

            for (int side = 0; side < 2; side++)
            {
                var color = side == 0 ? new Color(1f, 0.06f, 0.08f) : new Color(0.12f, 0.35f, 1f);
                var lampGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lampGo.name = side == 0 ? "LightBar_Red" : "LightBar_Blue";
                DestroyCollider(lampGo);
                lampGo.transform.SetParent(barRoot.transform, false);
                lampGo.transform.localPosition = new Vector3(side == 0 ? -0.34f : 0.34f, 0.02f, 0f);
                lampGo.transform.localScale = new Vector3(0.6f, 0.1f, 0.3f);

                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = side == 0 ? "Police_LampRed" : "Police_LampBlue";
                mat.color = color;
                mat.EnableKeyword("_EMISSION");
                if (lightBarEmission != null) mat.SetTexture(BaseMapId, lightBarEmission);
                mat.SetColor(EmissionId, color * 6f);
                mat.SetFloat("_Smoothness", 0.95f);
                var rend = lampGo.GetComponent<Renderer>();
                rend.sharedMaterial = mat;
                (side == 0 ? _redRenderers : _blueRenderers).Add(rend);

                // источник света
                var lightGo = new GameObject(side == 0 ? "SpotRed" : "SpotBlue");
                lightGo.transform.SetParent(barRoot.transform, false);
                lightGo.transform.localPosition = new Vector3(side == 0 ? -0.34f : 0.34f, 0.08f, 0f);
                lightGo.transform.localRotation = Quaternion.Euler(72f, side == 0 ? -18f : 18f, 0f);
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = color;
                l.range = 45f;
                l.spotAngle = 120f;
                l.innerSpotAngle = 70f;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                (side == 0 ? _red : _blue).Add(l);
            }
        }

        // ---------------------------------------------------------------- decals
        private void BuildDecals(Transform parent)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Decal_" + (side < 0 ? "Left" : "Right");
                DestroyCollider(quad);
                quad.transform.SetParent(parent, false);
                quad.transform.localPosition = new Vector3(side * decalOffsetFromCenter, decalHeight, 0.05f);
                quad.transform.localRotation = Quaternion.Euler(0f, side * 90f, 0f);
                quad.transform.localScale = new Vector3(decalSize.x, decalSize.y, 1f);

                var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                mat.name = "Police_Decal";
                if (liveryTexture != null)
                {
                    mat.SetTexture(BaseMapId, liveryTexture);
                    mat.SetTexture("_MainTex", liveryTexture);
                }
                else
                {
                    mat.color = new Color(0.1f, 0.12f, 0.18f);
                }
                // альфа-смешивание, чтобы фон ливреи не «съедал» кузов
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_AlphaClip", 0f);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                var r = quad.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        // ---------------------------------------------------------------- extras
        private void BuildPushBar(Transform parent)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "PushBar";
            DestroyCollider(bar);
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = new Vector3(0f, 0.62f, 2.25f);
            bar.transform.localScale = new Vector3(1.75f, 0.62f, 0.09f);
            bar.GetComponent<Renderer>().sharedMaterial = MakeBlackPlastic();
        }

        private void BuildAntenna(Transform parent)
        {
            var antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antenna.name = "Antenna";
            DestroyCollider(antenna);
            antenna.transform.SetParent(parent, false);
            antenna.transform.localPosition = new Vector3(-0.6f, 1.62f, -1.2f);
            antenna.transform.localScale = new Vector3(0.025f, 0.42f, 0.025f);
            antenna.GetComponent<Renderer>().sharedMaterial = MakeBlackPlastic();
        }

        private static Material MakeBlackPlastic()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "Police_BlackPlastic";
            m.color = new Color(0.045f, 0.05f, 0.06f);
            m.SetFloat("_Metallic", 0.1f);
            m.SetFloat("_Smoothness", 0.55f);
            return m;
        }

        private static void DestroyCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c == null) return;
            // этот код выполняется и в редакторе (сборка префаба), и в рантайме:
            // DestroyImmediate в play-mode запрещён, поэтому выбираем по контексту
            if (Application.isPlaying) Object.Destroy(c);
            else Object.DestroyImmediate(c);
        }

        // ---------------------------------------------------------------- flashing
        public void SetSirens(bool on)
        {
            sirensOn = on;
            if (!on)
            {
                foreach (var l in _red) if (l) l.intensity = 0f;
                foreach (var l in _blue) if (l) l.intensity = 0f;
                SetEmissive(_redRenderers, 0.15f);
                SetEmissive(_blueRenderers, 0.15f);
            }
        }

        private void Update()
        {
            if (!sirensOn || !Application.isPlaying) return;
            _timer += Time.deltaTime;
            if (_timer < flashInterval) return;
            _timer = 0f;
            _phase = (_phase + 1) % 6;

            // «полицейская» последовательность: красный-красный, синий-синий, оба
            bool redOn = _phase < 2 || _phase == 5;
            bool blueOn = (_phase >= 2 && _phase < 5) || _phase == 5;
            foreach (var l in _red) if (l) l.intensity = redOn ? lightIntensity : 0f;
            foreach (var l in _blue) if (l) l.intensity = blueOn ? lightIntensity : 0f;
            SetEmissive(_redRenderers, redOn ? 9f : 0.2f);
            SetEmissive(_blueRenderers, blueOn ? 9f : 0.2f);
        }

        private static void SetEmissive(List<Renderer> renderers, float strength)
        {
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mat = r.sharedMaterial;
                if (mat == null) continue;
                // материал общий для всех экземпляров — правим цвет свечения от базового
                mat.SetColor(EmissionId, mat.color * strength);
            }
        }
    }
}
