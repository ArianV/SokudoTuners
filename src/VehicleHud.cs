using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
using ScheduleOne.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SokudoTuners
{
    // Racing-style speedometer with a curved nitro bar down its left side. It replaces the game's plain
    // speed text, which also freezes above ~126 km/h because the game's velocity smoother throws away
    // samples over 25 m/s as "unreasonable". I read the car's Rigidbody instead, which has no cap.
    //
    // It lives on the game's vehicle canvas, so it shows and hides with the vehicle HUD on its own.
    // All the round shapes are textures I draw in code at startup, so the mod stays a single DLL.
    public static class VehicleHud
    {
        // Layout, in the vehicle canvas' units (roughly pixels at 1080p).
        private const float DialSize = 230f;
        private const float ScreenMargin = 40f;

        // Angles are maths-style: degrees counter-clockwise from pointing right. The dial sweeps
        // clockwise from bottom-left (225) round to bottom-right (-45), leaving a gap at the bottom.
        private const float DialStartAngle = 225f;
        private const float DialSweep = 270f;

        // The nitro bar runs up the outside left of the dial, from bottom-left to top-left.
        private const float NitroStartAngle = 240f;
        private const float NitroSweep = 120f;

        private static readonly Color DialColor = new Color(0.04f, 0.04f, 0.06f, 0.78f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.12f);
        private static readonly Color SpeedArcColor = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color RedlineColor = new Color(1f, 0.25f, 0.2f);
        private static readonly Color NeedleColor = new Color(1f, 0.22f, 0.15f);
        private static readonly Color NitroTrackColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color NitroReadyColor = new Color(0.15f, 0.6f, 1f);
        private static readonly Color NitroBoostColor = new Color(0.45f, 0.95f, 1f);
        private static readonly Color NitroLowColor = new Color(0.45f, 0.45f, 0.5f);

        private static Sprite _discSprite;
        private static Sprite _thinRingSprite;
        private static Sprite _arcRingSprite;

        private static GameObject _root;
        private static bool _builtImperial;
        private static float _dialMax;
        private static TextMeshProUGUI _gameSpeedText;
        private static RectTransform _needle;
        private static Image _speedArc;
        private static Image _nitroFill;
        private static TextMeshProUGUI _speedNumber;
        private static float _shownSpeed;

        // Called every frame from the mod.
        public static void Update()
        {
            bool imperial = ModSettings.UseMph;

            // Rebuild if the unit setting changed, so the dial numbers match.
            if (_root != null && imperial != _builtImperial)
                Object.Destroy(_root);

            // Unity's == null is also true for objects destroyed by a scene change, so this rebuilds
            // the HUD after going back to the menu and loading in again.
            if (_root == null && !TryBuild(imperial))
                return;

            // Passengers see the vehicle HUD too, but the gauge is only for the driver.
            LandVehicle vehicle = LocalDriver.GetVehicle();
            _root.SetActive(vehicle != null);
            if (vehicle == null)
                return;

            // The game keeps writing its own speed text every frame, so I keep it switched off.
            if (_gameSpeedText != null)
                _gameSpeedText.enabled = false;

            float speed = ModSettings.DisplaySpeed(vehicle.Rb.velocity.magnitude);

            // Ease the needle towards the real value so it sweeps instead of twitching.
            _shownSpeed = Mathf.Lerp(_shownSpeed, speed, 1f - Mathf.Exp(-12f * Time.deltaTime));
            float t = Mathf.Clamp01(_shownSpeed / _dialMax);

            _needle.localEulerAngles = new Vector3(0f, 0f, DialStartAngle - DialSweep * t - 90f);
            _speedArc.fillAmount = DialSweep / 360f * t;
            _speedArc.color = NitroTank.IsBoosting ? NitroBoostColor : SpeedArcColor;
            _speedNumber.text = Mathf.RoundToInt(_shownSpeed).ToString();

            _nitroFill.fillAmount = NitroSweep / 360f * NitroTank.Fuel;
            if (NitroTank.IsBoosting)
                _nitroFill.color = NitroBoostColor;
            else if (NitroTank.Fuel < NitroTank.MinFuelToStart)
                _nitroFill.color = NitroLowColor;
            else
                _nitroFill.color = NitroReadyColor;
        }

        private static bool TryBuild(bool imperial)
        {
            if (!Singleton<VehicleCanvas>.InstanceExists)
                return false;

            VehicleCanvas vehicleCanvas = Singleton<VehicleCanvas>.Instance;
            _gameSpeedText = vehicleCanvas.SpeedText;
            if (vehicleCanvas.Canvas == null || _gameSpeedText == null)
                return false;

            BuildSprites();
            TMP_FontAsset font = _gameSpeedText.font;

            // Nitro maxes out around 320 km/h (200 mph) on the game's scale, so the dial goes a bit past that.
            _builtImperial = imperial;
            _dialMax = imperial ? 220f : 360f;
            float majorStep = imperial ? 20f : 40f;
            float minorStep = majorStep / 2f;

            _root = new GameObject("SokudoHud", typeof(RectTransform));
            RectTransform root = (RectTransform)_root.transform;
            root.SetParent(vehicleCanvas.Canvas.transform, false);
            root.anchorMin = root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(1f, 0f);
            root.anchoredPosition = new Vector2(-ScreenMargin, ScreenMargin);
            root.sizeDelta = new Vector2(DialSize, DialSize);

            float radius = DialSize / 2f;

            // Nitro bar first so the dial draws over its inner edge.
            float nitroSize = DialSize + 44f;
            AddArc(root, _arcRingSprite, nitroSize, NitroStartAngle, NitroSweep / 360f, NitroTrackColor);
            _nitroFill = AddArc(root, _arcRingSprite, nitroSize, NitroStartAngle, 0f, NitroReadyColor);
            AddText(root, font, "NITRO", 12f, PointOnCircle(NitroStartAngle + 12f, radius + 26f),
                new Vector2(60f, 16f), NitroReadyColor);

            AddImage(root, _discSprite, Vector2.zero, new Vector2(DialSize, DialSize), DialColor);
            AddImage(root, _thinRingSprite, Vector2.zero, new Vector2(DialSize, DialSize), new Color(1f, 1f, 1f, 0.25f));

            float arcSize = DialSize - 14f;
            AddArc(root, _arcRingSprite, arcSize, DialStartAngle, DialSweep / 360f, TrackColor);
            _speedArc = AddArc(root, _arcRingSprite, arcSize, DialStartAngle, 0f, SpeedArcColor);

            // Ticks and numbers. The top 20% of the dial is the red zone.
            for (float v = 0f; v <= _dialMax + 0.01f; v += minorStep)
            {
                float angle = DialStartAngle - DialSweep * (v / _dialMax);
                bool major = Mathf.Abs(v % majorStep) < 0.01f;
                Color color = v >= _dialMax * 0.8f ? RedlineColor : Color.white;

                Vector2 size = major ? new Vector2(3f, 12f) : new Vector2(2f, 6f);
                Image tick = AddImage(root, null, PointOnCircle(angle, radius - 24f - size.y / 2f), size,
                    major ? color : new Color(color.r, color.g, color.b, 0.5f));
                tick.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle - 90f);

                if (major)
                    AddText(root, font, v.ToString("0"), 15f, PointOnCircle(angle, radius - 48f),
                        new Vector2(40f, 20f), color);
            }

            // Needle grows up from the centre, so its pivot sits at its base.
            Image needle = AddImage(root, null, Vector2.zero, new Vector2(4f, radius - 30f), NeedleColor);
            _needle = needle.rectTransform;
            _needle.pivot = new Vector2(0.5f, 0f);
            AddImage(root, _discSprite, Vector2.zero, new Vector2(18f, 18f), NeedleColor);

            // Digital readout sits in the gap at the bottom, drawn last so the needle never covers it.
            _speedNumber = AddText(root, font, "0", 40f, new Vector2(0f, -42f), new Vector2(140f, 44f), Color.white);
            AddText(root, font, imperial ? "MPH" : "KM/H", 14f, new Vector2(0f, -70f), new Vector2(80f, 18f),
                new Color(1f, 1f, 1f, 0.7f));

            Melon<SokudoTunersMod>.Logger.Msg($"Speedometer built ({(imperial ? "mph" : "km/h")}, dial to {_dialMax}).");
            return true;
        }

        private static Vector2 PointOnCircle(float angleDegrees, float radius)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
        }

        // An arc is a ring sprite drawn as a radial fill. The fill starts at the top and runs clockwise,
        // so I rotate the whole image to move that start point to the angle I want.
        private static Image AddArc(RectTransform parent, Sprite ring, float size, float startAngle, float fill, Color color)
        {
            Image arc = AddImage(parent, ring, Vector2.zero, new Vector2(size, size), color);
            arc.type = Image.Type.Filled;
            arc.fillMethod = Image.FillMethod.Radial360;
            arc.fillOrigin = (int)Image.Origin360.Top;
            arc.fillClockwise = true;
            arc.fillAmount = fill;
            arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, startAngle - 90f);
            return arc;
        }

        private static Image AddImage(RectTransform parent, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject("Image", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            // Don't let the HUD swallow mouse clicks meant for the game.
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI AddText(RectTransform parent, TMP_FontAsset font, string value, float fontSize,
            Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        // Textures aren't tied to a scene, so these survive going back to the menu and I only draw them once.
        private static void BuildSprites()
        {
            if (_discSprite == null) _discSprite = MakeRingSprite(256, 0f);
            if (_thinRingSprite == null) _thinRingSprite = MakeRingSprite(256, 0.97f);
            if (_arcRingSprite == null) _arcRingSprite = MakeRingSprite(256, 0.88f);
        }

        // White ring (or a full disc when innerRatio is 0) with soft one-pixel edges so it isn't jagged.
        // The Image tints it to whatever colour I need.
        private static Sprite MakeRingSprite(int size, float innerRatio)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            float outer = size / 2f;
            float inner = outer * innerRatio;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - outer;
                    float dy = y + 0.5f - outer;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    float alpha = Mathf.Clamp01(outer - distance);
                    if (innerRatio > 0f)
                        alpha *= Mathf.Clamp01(distance - inner);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
