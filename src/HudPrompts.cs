using HarmonyLib;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI;
using ScheduleOne.UI.Input;
using ScheduleOne.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SokudoTuners
{
    // My key prompts, stacked above the game's own ones: SHIFT / Nitro once a car has a kit (lights up while boosting),
    // and F4 / Sokudo Tuners while I'm parked in the shop.
    public static class HudPrompts
    {
        private static readonly AccessTools.FieldRef<InputPromptsManager, InputPromptsUI> PromptsUI =
            AccessTools.FieldRefAccess<InputPromptsManager, InputPromptsUI>("_inputPromptsUI");
        private static readonly AccessTools.FieldRef<InputPromptsUI, Transform> BottomLeftPrompts =
            AccessTools.FieldRefAccess<InputPromptsUI, Transform>("_promptsBottomLeftInGameContainer");

        private static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        private static readonly Color Pink = new Color(1f, 0.16f, 0.52f);
        private static readonly Color Empty = new Color(1f, 0.3f, 0.3f);
        private static readonly Color ChipIdle = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color TextIdle = new Color(1f, 1f, 1f, 0.6f);

        private const float ChipHeight = 32f;
        private const float Gap = 8f;
        private const float GlowPadding = 18f;
        private static readonly Vector2 FallbackPosition = new Vector2(42f, 300f);

        private class Chip
        {
            public RectTransform Rect;
            public RectTransform KeyRect;
            public Image Key;
            public Image Glow;
            public TextMeshProUGUI KeyText;
            public TextMeshProUGUI Label;
        }

        private static GameObject _root;
        private static RectTransform _rect;
        private static Chip _nitro;
        private static Chip _tune;
        private static float _lit;
        private static float _emptyFlash;
        private static readonly Vector3[] _corners = new Vector3[4];

        public static void Update()
        {
            if (_root == null && !TryBuild())
                return;

            LandVehicle vehicle = LocalDriver.GetVehicle();
            bool driving = vehicle != null && !TuningMenu.IsOpen && !ShopCutscene.Playing;
            bool showNitro = driving && Upgrades.HasNitro(vehicle);
            bool showTune = driving && AutoShop.IsInBay(vehicle);

            _root.SetActive(showNitro || showTune);
            _nitro.Rect.gameObject.SetActive(showNitro);
            _tune.Rect.gameObject.SetActive(showTune);
            if (!showNitro)
            {
                _lit = 0f;
                _emptyFlash = 0f;
            }
            if (!showNitro && !showTune)
                return;

            PlaceAbovePrompts();
            float y = 0f;
            if (showNitro)
            {
                _nitro.Rect.anchoredPosition = new Vector2(0f, y);
                y += ChipHeight + Gap;
                AnimateNitro();
            }
            if (showTune)
            {
                _tune.Rect.anchoredPosition = new Vector2(0f, y);
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
                _tune.Glow.color = new Color(Pink.r, Pink.g, Pink.b, 0.25f + 0.25f * pulse);
            }
        }

        private static void AnimateNitro()
        {
            Keyboard keyboard = Keyboard.current;
            bool held = keyboard != null && keyboard.leftShiftKey.isPressed && !GameInput.IsTyping;
            bool boosting = NitroTank.IsBoosting;
            float dt = Time.unscaledDeltaTime;

            _lit = Mathf.MoveTowards(_lit, boosting ? 1f : 0f, dt * (boosting ? 12f : 4f));
            _emptyFlash = held && !boosting ? 1f : Mathf.MoveTowards(_emptyFlash, 0f, dt * 3f);

            float pulse = boosting ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;

            Color key = Color.Lerp(ChipIdle, new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f), _lit);
            _nitro.Key.color = Color.Lerp(key, new Color(Empty.r, Empty.g, Empty.b, 0.55f), _emptyFlash * (1f - _lit));
            _nitro.KeyText.color = Color.Lerp(TextIdle, new Color(0.05f, 0.05f, 0.1f), _lit);
            _nitro.Label.color = Color.Lerp(Color.Lerp(TextIdle, Empty, _emptyFlash), Cyan, _lit);
            _nitro.Glow.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f * _lit * pulse);
            _nitro.KeyRect.localScale = Vector3.one * (1f + 0.08f * _lit);
        }

        // Measures the prompts actually on screen each frame, so it stays put as the list changes (passenger, gamepad...).
        private static void PlaceAbovePrompts()
        {
            Transform container = null;
            if (Singleton<InputPromptsManager>.InstanceExists)
            {
                InputPromptsUI ui = PromptsUI(Singleton<InputPromptsManager>.Instance);
                if (ui != null)
                    container = BottomLeftPrompts(ui);
            }

            float top = float.MinValue;
            float left = float.MaxValue;
            if (container != null)
            {
                foreach (Transform child in container)
                {
                    RectTransform rect = child as RectTransform;
                    if (rect == null || !child.gameObject.activeInHierarchy)
                        continue;
                    rect.GetWorldCorners(_corners);
                    top = Mathf.Max(top, _corners[1].y);
                    left = Mathf.Min(left, _corners[0].x);
                }
            }

            // Nothing found, or a stretched container that would put me off the top of the screen.
            if (top == float.MinValue || top > Screen.height * 0.8f)
            {
                _rect.anchoredPosition = FallbackPosition;
                return;
            }

            _rect.position = new Vector3(left, top + 10f * _rect.lossyScale.y, _rect.position.z);
        }

        private static bool TryBuild()
        {
            if (!Singleton<VehicleCanvas>.InstanceExists)
                return false;
            VehicleCanvas canvas = Singleton<VehicleCanvas>.Instance;
            if (canvas.Canvas == null || canvas.SpeedText == null)
                return false;
            TMP_FontAsset font = canvas.SpeedText.font;

            _root = new GameObject("SokudoPrompts", typeof(RectTransform));
            _rect = (RectTransform)_root.transform;
            _rect.SetParent(canvas.Canvas.transform, false);
            _rect.anchorMin = _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;
            _rect.sizeDelta = new Vector2(260f, ChipHeight);
            _rect.anchoredPosition = FallbackPosition;

            _nitro = MakeChip(font, "SHIFT", "Nitro", 74f);
            _tune = MakeChip(font, "F4", "Sokudo Tuners", 46f);
            _tune.Key.color = new Color(Pink.r, Pink.g, Pink.b, 0.85f);
            _tune.KeyText.color = Color.white;
            _tune.Label.color = Color.white;

            _root.SetActive(false);
            return true;
        }

        private static Chip MakeChip(TMP_FontAsset font, string key, string label, float keyWidth)
        {
            Chip chip = new Chip();
            chip.Rect = Node(_rect, Vector2.zero, new Vector2(260f, ChipHeight), Vector2.zero);

            Vector2 keyCentre = new Vector2(keyWidth / 2f, ChipHeight / 2f);
            int glowWidth = (int)(keyWidth + GlowPadding * 2f);
            int glowHeight = (int)(ChipHeight + GlowPadding * 2f);
            chip.Glow = AddImage(Node(chip.Rect, keyCentre, new Vector2(glowWidth, glowHeight), new Vector2(0.5f, 0.5f)),
                RoundedSprite(glowWidth, glowHeight, 7f + GlowPadding, GlowPadding));
            chip.Glow.color = Color.clear;

            chip.KeyRect = Node(chip.Rect, keyCentre, new Vector2(keyWidth, ChipHeight), new Vector2(0.5f, 0.5f));
            chip.Key = AddImage(chip.KeyRect, RoundedSprite((int)keyWidth, (int)ChipHeight, 7f, 0f));
            chip.Key.color = ChipIdle;

            chip.KeyText = AddText(chip.KeyRect, font, key, 15f, new Vector2(keyWidth / 2f, ChipHeight / 2f),
                new Vector2(keyWidth, ChipHeight), TextAlignmentOptions.Center);
            chip.Label = AddText(chip.Rect, font, label, 19f, new Vector2(keyWidth + 10f + 100f, ChipHeight / 2f),
                new Vector2(200f, ChipHeight), TextAlignmentOptions.Left);
            return chip;
        }

        // Positions are measured from the parent's bottom-left corner.
        private static RectTransform Node(RectTransform parent, Vector2 position, Vector2 size, Vector2 pivot)
        {
            RectTransform rect = (RectTransform)new GameObject("Node", typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI AddText(RectTransform parent, TMP_FontAsset font, string value, float size, Vector2 centre,
            Vector2 box, TextAlignmentOptions alignment)
        {
            TextMeshProUGUI text = Node(parent, centre, box, new Vector2(0.5f, 0.5f)).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold | FontStyles.Italic;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = TextIdle;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        // Rounded rectangle from a signed distance. With a softness it fades out past the edge, which makes the glow.
        private static Sprite RoundedSprite(int width, int height, float radius, float softness)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];

            float halfW = width / 2f - softness;
            float halfH = height / 2f - softness;
            float r = Mathf.Min(radius - softness, Mathf.Min(halfW, halfH));

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float qx = Mathf.Abs(x + 0.5f - width / 2f) - (halfW - r);
                    float qy = Mathf.Abs(y + 0.5f - height / 2f) - (halfH - r);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;

                    float alpha = softness > 0f
                        ? Mathf.Pow(Mathf.Clamp01(1f - Mathf.Max(distance, 0f) / softness), 2f)
                        : Mathf.Clamp01(0.5f - distance);
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
