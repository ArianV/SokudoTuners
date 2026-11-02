using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.UI;
using ScheduleOne.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SokudoTuners
{
    // The nitro bar. I hang it under the game's own speedometer text, so it shows and hides with the
    // vehicle HUD (including when a menu is open) and uses the game's font without me managing any of that.
    public static class NitroHud
    {
        private static readonly Color BackgroundColor = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color ReadyColor = new Color(0.15f, 0.6f, 1f);
        private static readonly Color BoostingColor = new Color(0.4f, 0.95f, 1f);
        private static readonly Color LowColor = new Color(0.45f, 0.45f, 0.5f);

        private static GameObject _root;
        private static RectTransform _fill;
        private static Image _fillImage;

        // Called every frame from the mod.
        public static void Update()
        {
            // Unity's == null is also true for objects destroyed by a scene change, so this rebuilds
            // the bar after going back to the menu and loading in again.
            if (_root == null && !TryBuild())
                return;

            _root.SetActive(IsLocalPlayerDriving());

            _fill.anchorMax = new Vector2(NitroTank.Fuel, 1f);
            if (NitroTank.IsBoosting)
                _fillImage.color = BoostingColor;
            else if (NitroTank.Fuel < NitroTank.MinFuelToStart)
                _fillImage.color = LowColor;
            else
                _fillImage.color = ReadyColor;
        }

        // Passengers see the vehicle HUD too, but the bar is only useful to the driver.
        private static bool IsLocalPlayerDriving()
        {
            if (Player.Local == null || Player.Local.CurrentVehicle == null)
                return false;

            LandVehicle vehicle = Player.Local.CurrentVehicle.GetComponent<LandVehicle>();
            return vehicle != null && vehicle.LocalPlayerIsDriver;
        }

        private static bool TryBuild()
        {
            if (!Singleton<VehicleCanvas>.InstanceExists)
                return false;

            TextMeshProUGUI speedText = Singleton<VehicleCanvas>.Instance.SpeedText;
            if (speedText == null)
                return false;

            // Bar sits centred just under the speed number.
            _root = new GameObject("SokudoNitroBar", typeof(RectTransform));
            RectTransform rootRect = (RectTransform)_root.transform;
            rootRect.SetParent(speedText.transform, false);
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -6f);
            rootRect.sizeDelta = new Vector2(220f, 16f);
            AddImage(_root, BackgroundColor);

            // The fill stretches from the left edge to Fuel along the width. Changing anchorMax.x is all
            // it takes to move it, so I don't need a sprite.
            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            _fill = (RectTransform)fill.transform;
            _fill.SetParent(rootRect, false);
            _fill.anchorMin = Vector2.zero;
            _fill.anchorMax = Vector2.one;
            _fill.offsetMin = new Vector2(2f, 2f);
            _fill.offsetMax = new Vector2(-2f, -2f);
            _fillImage = AddImage(fill, ReadyColor);

            GameObject label = new GameObject("Label", typeof(RectTransform));
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rootRect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = label.AddComponent<TextMeshProUGUI>();
            text.font = speedText.font;
            text.fontSize = 12f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.text = "NITRO";
            text.raycastTarget = false;

            // So I can nudge the position later if it clashes with the HUD layout.
            RectTransform speedRect = speedText.rectTransform;
            Melon<SokudoTunersMod>.Logger.Msg(
                $"Nitro bar attached under speed text (anchors {speedRect.anchorMin}-{speedRect.anchorMax}, " +
                $"pos {speedRect.anchoredPosition}, size {speedRect.sizeDelta})");
            return true;
        }

        private static Image AddImage(GameObject go, Color color)
        {
            Image image = go.AddComponent<Image>();
            image.color = color;
            // Don't let the bar swallow mouse clicks meant for the game.
            image.raycastTarget = false;
            return image;
        }
    }
}
