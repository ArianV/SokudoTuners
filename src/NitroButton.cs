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
    // SHIFT / NITRO prompt that sits above the game's own key prompts once a car has a nitro kit, and lights up while boosting.
    public static class NitroButton
    {
        private static readonly AccessTools.FieldRef<InputPromptsManager, InputPromptsUI> PromptsUI =
            AccessTools.FieldRefAccess<InputPromptsManager, InputPromptsUI>("_inputPromptsUI");
        private static readonly AccessTools.FieldRef<InputPromptsUI, Transform> BottomLeftPrompts =
            AccessTools.FieldRefAccess<InputPromptsUI, Transform>("_promptsBottomLeftInGameContainer");

        private static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        private static readonly Color Empty = new Color(1f, 0.3f, 0.3f);
        private static readonly Color ChipIdle = new Color(1f, 1f, 1f, 0.16f);
        private static readonly Color TextIdle = new Color(1f, 1f, 1f, 0.6f);

        private const float ChipWidth = 74f;
        private const float ChipHeight = 32f;
        private const float GlowPadding = 18f;
        private static readonly Vector2 FallbackPosition = new Vector2(42f, 300f);

        private static GameObject _root;
        private static RectTransform _rect;
        private static RectTransform _chipRect;
        private static Image _chip;
        private static Image _glow;
        private static TextMeshProUGUI _key;
        private static TextMeshProUGUI _label;
        private static float _lit;
        private static float _emptyFlash;
        private static readonly Vector3[] _corners = new Vector3[4];

        public static void Update()
        {
            if (_root == null && !TryBuild())
                return;

            LandVehicle vehicle = LocalDriver.GetVehicle();
            bool show = vehicle != null && !TuningMenu.IsOpen && Upgrades.HasNitro(vehicle);
            _root.SetActive(show);
            if (!show)
            {
                _lit = 0f;
                _emptyFlash = 0f;
                return;
            }

            PlaceAbovePrompts();

            Keyboard keyboard = Keyboard.current;
            bool held = keyboard != null && keyboard.leftShiftKey.isPressed && !GameInput.IsTyping;
            bool boosting = NitroTank.IsBoosting;
            float dt = Time.unscaledDeltaTime;

            _lit = Mathf.MoveTowards(_lit, boosting ? 1f : 0f, dt * (boosting ? 12f : 4f));
            _emptyFlash = held && !boosting ? 1f : Mathf.MoveTowards(_emptyFlash, 0f, dt * 3f);

            float pulse = boosting ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;

            Color chip = Color.Lerp(ChipIdle, new Color(Cyan.r, Cyan.g, Cyan.b, 0.95f), _lit);
            _chip.color = Color.Lerp(chip, new Color(Empty.r, Empty.g, Empty.b, 0.55f), _emptyFlash * (1f - _lit));
            _key.color = Color.Lerp(TextIdle, new Color(0.05f, 0.05f, 0.1f), _lit);
            _label.color = Color.Lerp(Color.Lerp(TextIdle, Empty, _emptyFlash), Cyan, _lit);
            _glow.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f * _lit * pulse);
            _chipRect.localScale = Vector3.one * (1f + 0.08f * _lit);
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

            _root = new GameObject("SokudoNitroButton", typeof(RectTransform));
            _rect = (RectTransform)_root.transform;
            _rect.SetParent(canvas.Canvas.transform, false);
            _rect.anchorMin = _rect.anchorMax = Vector2.zero;
            _rect.pivot = Vector2.zero;
            _rect.sizeDelta = new Vector2(200f, ChipHeight);
            _rect.anchoredPosition = FallbackPosition;

            Vector2 chipCentre = new Vector2(ChipWidth / 2f, ChipHeight / 2f);
            int glowWidth = (int)(ChipWidth + GlowPadding * 2f);
            int glowHeight = (int)(ChipHeight + GlowPadding * 2f);
            _glow = AddImage(Node(_rect, chipCentre, new Vector2(glowWidth, glowHeight)),
                RoundedSprite(glowWidth, glowHeight, 7f + GlowPadding, GlowPadding));

            _chipRect = Node(_rect, chipCentre, new Vector2(ChipWidth, ChipHeight));
            _chip = AddImage(_chipRect, RoundedSprite((int)ChipWidth, (int)ChipHeight, 7f, 0f));

            _key = AddText(_chipRect, font, "SHIFT", 15f, Vector2.zero, new Vector2(ChipWidth, ChipHeight), TextAlignmentOptions.Center);
            _label = AddText(_rect, font, "Nitro", 19f, new Vector2(ChipWidth + 10f + 60f, ChipHeight / 2f), new Vector2(120f, ChipHeight),
                TextAlignmentOptions.Left);

            _root.SetActive(false);
            return true;
        }

        private static RectTransform Node(RectTransform parent, Vector2 centre, Vector2 size)
        {
            RectTransform rect = (RectTransform)new GameObject("Node", typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = parent == _rect ? Vector2.zero : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = centre;
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
            TextMeshProUGUI text = Node(parent, centre, box).gameObject.AddComponent<TextMeshProUGUI>();
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
