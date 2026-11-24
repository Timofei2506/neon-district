using UnityEngine;

namespace NeonDistrict.UI
{
    /// <summary>
    /// Процедурные спрайты и текстуры для UI: без внешних ассетов, всё генерируется кодом
    /// (в билд не нужно тащить png, а вид получается «дороже» плоских прямоугольников).
    /// Все текстуры кешируются статически, поэтому память не течёт при перестройке UI.
    /// </summary>
    public static class NeonGfx
    {
        private static Sprite _white, _roundRect, _roundRectOutline, _radialGlow, _vignette, _hLine, _arrow, _dot, _gradientVertical;
        private static Texture2D _scanlineTexture;

        /// <summary>1×1 белый спрайт — для заливок.</summary>
        public static Sprite White
        {
            get
            {
                if (_white == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    _white = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
                    _white.name = "ND_White";
                }
                return _white;
            }
        }

        /// <summary>Скруглённый прямоугольник 64×64 (9-slice через Sprite.Create с border).</summary>
        public static Sprite RoundRect
        {
            get
            {
                if (_roundRect == null) _roundRect = MakeRounded(64, 14, false);
                return _roundRect;
            }
        }

        /// <summary>Только контур скруглённого прямоугольника — для рамок и подсветки.</summary>
        public static Sprite RoundRectOutline
        {
            get
            {
                if (_roundRectOutline == null) _roundRectOutline = MakeRounded(64, 14, true);
                return _roundRectOutline;
            }
        }

        /// <summary>Радиальное свечение (белое в центре → прозрачное к краю), для неоновых бликов.</summary>
        public static Sprite RadialGlow
        {
            get
            {
                if (_radialGlow == null) _radialGlow = MakeRadial(128, 2f);
                return _radialGlow;
            }
        }

        /// <summary>Виньетка на весь экран: прозрачный центр, тёмные края.</summary>
        public static Sprite Vignette
        {
            get
            {
                if (_vignette == null) _vignette = MakeVignette(256);
                return _vignette;
            }
        }

        /// <summary>Горизонтальная линия с растворяющимися краями — аккуратный разделитель.</summary>
        public static Sprite SoftLine
        {
            get
            {
                if (_hLine == null) _hLine = MakeSoftLine(128);
                return _hLine;
            }
        }

        /// <summary>Треугольный указатель (курсор меню / маркер на миникарте).</summary>
        public static Sprite Arrow
        {
            get
            {
                if (_arrow == null) _arrow = MakeArrow(48);
                return _arrow;
            }
        }

        /// <summary>Мягкая точка — маркеры, «пылинки» в фоне.</summary>
        public static Sprite Dot
        {
            get
            {
                if (_dot == null) _dot = MakeRadial(32, 1.6f);
                return _dot;
            }
        }

        /// <summary>Вертикальный градиент: белый сверху → прозрачный снизу (для «тумана» и подложек).</summary>
        public static Sprite GradientVertical
        {
            get
            {
                if (_gradientVertical == null) _gradientVertical = MakeVerticalGradient(4, 128);
                return _gradientVertical;
            }
        }

        /// <summary>Текстура горизонтального градиента 256×1 — используется как colorGradient у Image.</summary>
        public static Texture2D NeonGradientTexture(params Color[] stops)
        {
            int w = 256;
            var tex = new Texture2D(w, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int x = 0; x < w; x++)
            {
                float t = x / (float)(w - 1);
                float f = t * (stops.Length - 1);
                int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, stops.Length - 2);
                tex.SetPixel(x, 0, Color.Lerp(stops[i], stops[i + 1], f - i));
            }
            tex.Apply();
            return tex;
        }

        /// <summary>Тонкие горизонтальные скан-линии поверх экрана — «дисплейный» вид.</summary>
        public static Texture2D Scanlines
        {
            get
            {
                if (_scanlineTexture == null)
                {
                    const int h = 4;
                    _scanlineTexture = new Texture2D(4, h, TextureFormat.RGBA32, false)
                    {
                        wrapMode = TextureWrapMode.Repeat,
                        filterMode = FilterMode.Point,
                    };
                    for (int y = 0; y < h; y++)
                    {
                        var c = new Color(0f, 0f, 0f, y == 0 ? 0.30f : 0f);
                        for (int x = 0; x < 4; x++) _scanlineTexture.SetPixel(x, y, c);
                    }
                    _scanlineTexture.Apply();
                }
                return _scanlineTexture;
            }
        }

        // ------------------------------------------------------------------ builders
        private static Sprite MakeRounded(int size, int radius, bool outlineOnly)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0f, x - (size - 1 - radius));
                    float dy = Mathf.Max(radius - y, 0f, y - (size - 1 - radius));
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha;
                    if (outlineOnly)
                    {
                        // рамка толщиной ~2 px по краю скругления
                        const float thickness = 2.2f;
                        alpha = Mathf.Clamp01(1f - Mathf.Abs(dist - radius) / thickness);
                        if (dx <= 0f || dy <= 0f)
                        {
                            // прямые участки: линии по краям
                            bool nearEdge = x < thickness || y < thickness || x > size - 1 - thickness || y > size - 1 - thickness;
                            alpha = nearEdge ? 1f : 0f;
                        }
                    }
                    else
                    {
                        alpha = Mathf.Clamp01(radius - dist + 0.5f);
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            int b = radius + 1;
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            sprite.name = outlineOnly ? "ND_RoundOutline" : "ND_RoundRect";
            return sprite;
        }

        private static Sprite MakeRadial(int size, float power)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, power);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            s.name = "ND_Radial";
            return s;
        }

        private static Sprite MakeVignette(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - half) / half;
                    float dy = (y + 0.5f - half) / half;
                    // эллипс: края затемняются сильнее по углам
                    float d = Mathf.Sqrt(dx * dx * 0.85f + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.75f));
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            s.name = "ND_Vignette";
            return s;
        }

        private static Sprite MakeSoftLine(int width)
        {
            var tex = new Texture2D(width, 2, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float t = x / (float)(width - 1);
                    float a = Mathf.Sin(t * Mathf.PI);   // 0 → 1 → 0
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(a, 0.7f)));
                }
            }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, width, 2), new Vector2(0.5f, 0.5f), 100f);
            s.name = "ND_SoftLine";
            return s;
        }

        private static Sprite MakeArrow(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // острие вправо: |y - center| < x * slope
                    float cy = size * 0.5f;
                    float halfWidth = (1f - x / (float)size) * cy * 0.95f;
                    float a = Mathf.Clamp01(halfWidth - Mathf.Abs(y - cy) + 1f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            s.name = "ND_Arrow";
            return s;
        }

        private static Sprite MakeVerticalGradient(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < height; y++)
            {
                float a = y / (float)(height - 1);
                for (int x = 0; x < width; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            s.name = "ND_GradientV";
            return s;
        }
    }
}
