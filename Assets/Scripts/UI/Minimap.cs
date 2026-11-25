using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NeonDistrict.Core;
using NeonDistrict.Player;
using NeonDistrict.Police;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Миникарта в стиле городского боевика: отдельная ортографическая камера сверху
    /// рендерит город в RenderTexture 512², кадр выводится в UGUI RawImage и
    /// поворачивается вслед за игроком. Поверх — метки: цель миссии, патрули, игрок.
    ///
    /// Все спрайты-метки генерируются кодом (никаких ассетов в репозитории).
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class Minimap : MonoBehaviour
    {
        [Header("Ссылки")]
        public Transform player;
        public Camera mapCamera;
        public RectTransform blipRoot;
        public RawImage output;

        [Header("Вид")]
        [Tooltip("Высота камеры над игроком, м")] public float height = 260f;
        [Tooltip("Размер видимой области, м")] public float orthoSize = 150f;
        [Tooltip("Поворачивать карту вслед за игроком")] public bool rotateWithPlayer = true;
        [Tooltip("Разрешение текстуры карты")] public int resolution = 512;

        [Header("Метки")]
        public Color missionColor = new Color(1f, 0.18f, 0.47f);
        public Color policeColor = new Color(0.26f, 0.62f, 1f);
        public Color playerColor = new Color(1f, 0.95f, 0.35f);

        private RenderTexture _rt;
        private readonly List<Blip> _blips = new List<Blip>();
        private Sprite _dotSprite;
        private Sprite _arrowSprite;
        private Image _playerArrow;
        private float _yaw;

        private class Blip
        {
            public Image image;
            public Transform target;
            public Vector3 staticPoint;
            public bool isStatic;
        }

        private void Awake()
        {
            _dotSprite = MakeDot(24, 8);
            _arrowSprite = MakeArrow(28);
        }

        private void Start()
        {
            if (player == null && GameManager.Instance != null && GameManager.Instance.player != null)
                player = GameManager.Instance.player.transform;

            var rt = EnsureRenderTexture();
            if (output != null) output.texture = rt;
            if (blipRoot != null) _playerArrow = CreateBlip("Player", playerColor, _arrowSprite, new Vector2(16f, 16f));

            if (mapCamera != null)
            {
                mapCamera.orthographic = true;
                mapCamera.orthographicSize = orthoSize;
                mapCamera.targetTexture = rt;   // в headless-режиме null: камера просто не рендерит
                mapCamera.clearFlags = CameraClearFlags.SolidColor;
                mapCamera.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
                mapCamera.farClipPlane = 900f;
                mapCamera.nearClipPlane = 0.5f;
                mapCamera.cullingMask = ~0;
                mapCamera.depth = -20;
            }
        }

        private RenderTexture EnsureRenderTexture()
        {
            if (_rt != null) return _rt;

            // В headless-режиме (-nographics, так гоняются автотесты в CI) графического устройства
            // нет и RenderTexture.Create падает с ошибкой. Мини-карта там не нужна — просто без неё.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return null;

            _rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.Default);
            _rt.name = "MinimapRT";
            _rt.filterMode = FilterMode.Bilinear;
            if (!_rt.Create())
            {
                Debug.LogWarning("[Neon District] мини-карта: RenderTexture не создалась — работаем без неё");
                return _rt;
            }
            return _rt;
        }

        private void LateUpdate()
        {
            if (player == null) return;
            var t = player;

            if (rotateWithPlayer)
            {
                // в машине ориентируемся по машине, пешком — по направлению взгляда
                var pc = t.GetComponent<PlayerController>();
                Transform refT = pc != null && pc.IsInVehicle && pc.currentVehicle != null ? pc.currentVehicle.transform : t;
                _yaw = refT.eulerAngles.y;
            }
            else _yaw = 0f;

            if (mapCamera != null)
            {
                var pos = t.position;
                mapCamera.transform.position = new Vector3(pos.x, pos.y + height, pos.z);
                mapCamera.transform.rotation = Quaternion.Euler(90f, _yaw, 0f);
            }

            if (_playerArrow != null && rotateWithPlayer)
                _playerArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            UpdateBlips();
        }

        // ---------------------------------------------------------------- метки
        private Image CreateBlip(string name, Color color, Sprite sprite, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(blipRoot, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Постоянная метка на объект (например, все полицейские машины).</summary>
        public void Track(Transform target, Color color)
        {
            if (blipRoot == null || target == null) return;
            var img = CreateBlip("Blip_" + target.name, color, _dotSprite, new Vector2(10f, 10f));
            _blips.Add(new Blip { image = img, target = target });
        }

        public void ClearTracked()
        {
            foreach (var b in _blips)
                if (b.image != null) Destroy(b.image.gameObject);
            _blips.Clear();
        }

        private void UpdateBlips()
        {
            // полицейские патрули — метки создаём по факту появления
            var police = FindObjectsByType<PoliceAI>(FindObjectsSortMode.None);
            foreach (var p in police)
            {
                if (p == null) continue;
                bool known = false;
                foreach (var b in _blips) if (b.target == p.transform) { known = true; break; }
                if (!known) Track(p.transform, policeColor);
            }
        }

        /// <summary>Перевод мировой точки в координаты миникарты (0..1).</summary>
        public bool WorldToMap(Vector3 world, out Vector2 uv)
        {
            uv = Vector2.zero;
            if (player == null) return false;
            var d = world - player.position;
            float yawRad = _yaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(yawRad), sin = Mathf.Sin(yawRad);
            // поворот в плоскость карты: карта смотрит «вверх» = forward игрока
            float x = d.x * cos - d.z * sin;
            float z = d.x * sin + d.z * cos;
            const float scale = 2f;   // запас за границей круга
            uv = new Vector2(0.5f + x / (orthoSize * 2f) * scale * 0.5f,
                             0.5f + z / (orthoSize * 2f) * scale * 0.5f);
            return uv.x > -0.2f && uv.x < 1.2f && uv.y > -0.2f && uv.y < 1.2f;
        }

        /// <summary>Метка цели миссии: единственная, обновляется каждую секунду.</summary>
        public void SetObjectiveMarker(Vector3 world, bool visible)
        {
            if (_objective == null && visible && blipRoot != null)
                _objective = CreateBlip("Objective", missionColor, _dotSprite, new Vector2(14f, 14f));
            if (_objective == null) return;
            _objective.enabled = visible;
            if (!visible) return;

            var rt = blipRoot;
            float w = rt.rect.width, h = rt.rect.height;
            if (WorldToMap(world, out var uv))
            {
                _objective.rectTransform.anchoredPosition = new Vector2((uv.x - 0.5f) * w, (uv.y - 0.5f) * h);
            }
            else
            {
                // цель далеко — прижимаем метку к краю (как в GTA)
                var dir = world - player.position;
                var n = new Vector2(dir.x, dir.z).normalized;
                _objective.rectTransform.anchoredPosition = new Vector2(n.x * w * 0.42f, n.y * h * 0.42f);
            }
        }

        private Image _objective;

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); Destroy(_rt); }
        }

        // ---------------------------------------------------------------- спрайты
        private static Sprite MakeDot(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var c = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01((radius - d) / 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private static Sprite MakeArrow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float ny = (y - size * 0.5f) / (size * 0.5f);
                    float nx = (x - size * 0.5f) / (size * 0.5f);
                    // треугольник, направленный вверх
                    bool inside = ny > -0.6f && ny < 0.9f && Mathf.Abs(nx) < (0.9f - ny) * 0.75f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, inside ? 1f : 0f));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
