using System.Collections.Generic;
using MelonLoader;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.State;
using ScheduleOne.UI;
using ScheduleOne.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SokudoTuners
{
    // The garage. F4 in the driver's seat opens it. Racing-game style: part list down the left with the car
    // still in view on the right, tier pips, before/after stat bars, and a key prompt bar along the bottom.
    //
    // It joins the game's own state stack like a built-in menu, so while it's open the car ignores driving
    // input, the mouse is free, and Escape closes it instead of opening the pause menu.
    // Upgrades are free for now. Prices slot in next to the install button later.
    public static class TuningMenu
    {
        // NFS Heat-ish neon: hot pink for the selection and upgrades, cyan for what's installed (same as the nitro).
        private static readonly Color Pink = new Color(1f, 0.16f, 0.52f);
        private static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        private static readonly Color Downgrade = new Color(1f, 0.35f, 0.3f);
        private static readonly Color TileIdle = new Color(0.07f, 0.05f, 0.12f, 0.88f);
        private static readonly Color Backdrop = new Color(0.03f, 0.02f, 0.07f);
        private static readonly Color Dim = new Color(1f, 1f, 1f, 0.5f);
        private static readonly Color Empty = new Color(1f, 1f, 1f, 0.12f);

        private static readonly UpgradeType[] Categories =
            { UpgradeType.Engine, UpgradeType.Tires, UpgradeType.Suspension, UpgradeType.Brakes, UpgradeType.Nitro };

        private static readonly string[] CategoryBlurbs =
        {
            "Acceleration and top speed",
            "Traction and cornering grip",
            "Stability and high-speed downforce",
            "Stopping power",
            "Tank size, refill rate and boost"
        };

        // Layout, in 1080p reference units. The canvas scaler takes care of other resolutions.
        private const float Left = 70f;
        private const float TileTop = 240f;
        private const float TileStep = 84f;
        private const float DetailTop = 680f;
        private const float OpenAnimSeconds = 0.28f;

        public static bool IsOpen { get; private set; }

        private class Tile
        {
            public RectTransform Rect;
            public Image Background;
            public TextMeshProUGUI Number;
            public TextMeshProUGUI Name;
            public Image[] Pips;
            public float Offset;
        }

        private class StatRow
        {
            public GameObject Root;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Delta;
            public RectTransform Base;
            public RectTransform Change;
            public Image ChangeImage;
            public float ShownBase;
            public float ShownChange;
            public float TargetBase;
            public float TargetChange;
        }

        private static GameObject _root;
        private static Canvas _canvas;
        private static CanvasGroup _group;
        private static MonoState _state;
        private static RectTransform _content;
        private static TMP_FontAsset _font;
        private static GameInput.ExitDelegate _exitListener;

        private static readonly List<Tile> _tiles = new List<Tile>();
        private static readonly StatRow[] _stats = new StatRow[3];
        private static readonly Image[] _tierPips = new Image[Upgrades.MaxTier + 1];
        private static readonly TextMeshProUGUI[] _tierPipLabels = new TextMeshProUGUI[Upgrades.MaxTier + 1];
        private static TextMeshProUGUI _carName;
        private static TextMeshProUGUI _categoryTitle;
        private static TextMeshProUGUI _categoryBlurb;
        private static TextMeshProUGUI _tierName;
        private static TextMeshProUGUI _tierState;
        private static TextMeshProUGUI _description;
        private static Image _installButton;
        private static TextMeshProUGUI _installLabel;
        private static TextMeshProUGUI _unitsLabel;

        private static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        private static Sprite _notifyIcon;

        private static LandVehicle _vehicle;
        private static int _selected;
        private static int _preview;
        private static float _openedAt;
        private static float _installFlash;

        // Called every frame from the mod.
        public static void Update()
        {
            // The canvas dies with the scene. If that happens while it's open, reset so the next open starts clean.
            if (IsOpen && _root == null)
                OnClosed();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (!IsOpen)
            {
                if (keyboard.f4Key.wasPressedThisFrame && !GameInput.IsTyping)
                    TryOpen();
                return;
            }

            // Kicked out of the car somehow (arrested, knocked out). The menu has nothing to tune any more.
            if (LocalDriver.GetVehicle() != _vehicle)
            {
                Close();
                return;
            }

            HandleKeys(keyboard);
            Animate();
        }

        // ---------------------------------------------------------------- opening and closing

        private static void TryOpen()
        {
            LandVehicle vehicle = LocalDriver.GetVehicle();
            if (vehicle == null)
                return;
            if (Singleton<PauseMenu>.InstanceExists && Singleton<PauseMenu>.Instance.IsPaused)
                return;

            // The car coasts while the menu has the controls, so tuning at speed would mean crashing blind.
            float stopSpeed = ModSettings.UseMph ? 10f : 16f;
            if (ModSettings.DisplaySpeed(vehicle.Rb.velocity.magnitude) > stopSpeed)
            {
                Notify("Sokudo Tuners", "Come to a stop to open the garage");
                return;
            }

            if (_root == null && !Build())
                return;

            _vehicle = vehicle;
            _selected = 0;
            _preview = Upgrades.GetTier(vehicle, Categories[0]);
            _carName.text = vehicle.VehicleName.ToUpperInvariant();
            _installFlash = 0f;

            _state.PushToDefaultParent();
            _exitListener = OnExit;
            // The pause menu listens at -9999999. Anything higher hears Escape first.
            GameInput.RegisterExitListener(_exitListener, 10);

            _canvas.enabled = true;
            IsOpen = true;
            _openedAt = Time.unscaledTime;

            foreach (Tile tile in _tiles)
                tile.Offset = 0f;
            Refresh();
            SnapStatBars();
            Animate();
        }

        public static void Close()
        {
            if (!IsOpen)
                return;

            // Leaving the state stack hands control back to the car, which puts the mouse and HUD back the way it likes them.
            if (_state != null)
                _state.RemoveFromDefaultParent();
            OnClosed();
        }

        private static void OnClosed()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            _vehicle = null;
            if (_canvas != null)
                _canvas.enabled = false;
            if (_exitListener != null)
                GameInput.DeregisterExitListener(_exitListener);
        }

        private static void OnExit(ExitAction action)
        {
            if (!action.Used && IsOpen && action.Type == ExitType.Primary)
            {
                action.Used = true;
                Close();
            }
        }

        // ---------------------------------------------------------------- input and actions

        private static void HandleKeys(Keyboard keyboard)
        {
            if (keyboard.f4Key.wasPressedThisFrame || keyboard.backspaceKey.wasPressedThisFrame)
            {
                Close();
                return;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                Select(_selected - 1);
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                Select(_selected + 1);
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                SetPreview(_preview - 1);
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                SetPreview(_preview + 1);
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                Install();
            if (keyboard.uKey.wasPressedThisFrame)
                ToggleUnits();
        }

        private static void Select(int index)
        {
            _selected = (index + Categories.Length) % Categories.Length;
            _preview = Upgrades.GetTier(_vehicle, Categories[_selected]);
            Refresh();
        }

        private static void SetPreview(int tier)
        {
            _preview = Mathf.Clamp(tier, 0, Upgrades.MaxTier);
            Refresh();
        }

        private static void Install()
        {
            UpgradeType type = Categories[_selected];
            if (_preview == Upgrades.GetTier(_vehicle, type))
                return;

            Upgrades.SetTier(_vehicle, type, _preview);
            _installFlash = 1f;
            Melon<SokudoTunersMod>.Logger.Msg(
                $"Installed {Upgrades.TierName(_preview)} {type} on '{_vehicle.VehicleName}' ({Upgrades.Describe(type, _preview)})");
            Refresh();
        }

        private static void ToggleUnits()
        {
            ModSettings.UseMph = !ModSettings.UseMph;
            Refresh();
        }

        // Puts every label, pip and bar target in line with the current selection. Animate() does the easing.
        private static void Refresh()
        {
            for (int i = 0; i < _tiles.Count; i++)
            {
                int installed = Upgrades.GetTier(_vehicle, Categories[i]);
                for (int p = 0; p < _tiles[i].Pips.Length; p++)
                    _tiles[i].Pips[p].color = p < installed ? Cyan : Empty;
            }

            UpgradeType type = Categories[_selected];
            int current = Upgrades.GetTier(_vehicle, type);

            _categoryTitle.text = type.ToString().ToUpperInvariant();
            _categoryBlurb.text = CategoryBlurbs[_selected];
            _tierName.text = Upgrades.TierName(_preview).ToUpperInvariant();
            _tierName.color = _preview == current ? Cyan : Pink;

            if (_preview == current)
                _tierState.text = "INSTALLED";
            else
                _tierState.text = $"{Upgrades.TierName(current).ToUpperInvariant()}  >  {Upgrades.TierName(_preview).ToUpperInvariant()}";

            for (int t = 0; t <= Upgrades.MaxTier; t++)
            {
                Color color;
                if (t <= Mathf.Min(current, _preview))
                    color = Cyan;
                else if (t <= _preview)
                    color = Pink;
                else if (t <= current)
                    color = Downgrade;
                else
                    color = Empty;
                _tierPips[t].color = color;
                // The previewed tier stands taller so it's obvious which one A/D is on.
                _tierPips[t].rectTransform.localScale = new Vector3(1f, t == _preview ? 1.7f : 1f, 1f);
                _tierPipLabels[t].color = t == _preview ? Color.white : Dim;
            }

            UpgradeStat[] before = Upgrades.GetStats(type, current);
            UpgradeStat[] after = Upgrades.GetStats(type, _preview);
            for (int s = 0; s < _stats.Length; s++)
            {
                StatRow row = _stats[s];
                bool used = s < before.Length;
                row.Root.SetActive(used);
                if (!used)
                    continue;

                row.Label.text = before[s].Label;
                row.TargetBase = Mathf.Min(before[s].Value, after[s].Value);
                row.TargetChange = Mathf.Max(before[s].Value, after[s].Value);
                bool gain = after[s].Value >= before[s].Value;
                row.ChangeImage.color = gain ? Pink : Downgrade;

                float percent = (after[s].Value / before[s].Value - 1f) * 100f;
                row.Delta.text = Mathf.Abs(percent) < 0.5f ? "" : $"{(percent > 0f ? "+" : "")}{percent:0}%";
                row.Delta.color = gain ? Pink : Downgrade;
            }

            _description.text = Upgrades.Describe(type, _preview);
            _installLabel.text = _preview == current ? "INSTALLED" : "INSTALL  /  FREE";
            _unitsLabel.text = ModSettings.UseMph ? "UNITS: MPH" : "UNITS: KM/H";
        }

        // ---------------------------------------------------------------- animation

        private static void Animate()
        {
            float dt = Time.unscaledDeltaTime;

            // Slide in from the left and fade up, easing out so it lands softly.
            float t = Mathf.Clamp01((Time.unscaledTime - _openedAt) / OpenAnimSeconds);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            _content.anchoredPosition = new Vector2(Mathf.Lerp(-120f, 0f, eased), 0f);
            _group.alpha = eased;

            float ease = 1f - Mathf.Exp(-14f * dt);
            for (int i = 0; i < _tiles.Count; i++)
            {
                Tile tile = _tiles[i];
                bool selected = i == _selected;
                tile.Offset = Mathf.Lerp(tile.Offset, selected ? 28f : 0f, ease);
                tile.Rect.anchoredPosition = new Vector2(Left + tile.Offset, -(TileTop + i * TileStep));
                tile.Background.color = Color.Lerp(tile.Background.color, selected ? Pink : TileIdle, ease);
                tile.Number.color = selected ? Color.white : Dim;
            }

            foreach (StatRow row in _stats)
            {
                row.ShownBase = Mathf.Lerp(row.ShownBase, row.TargetBase, ease);
                row.ShownChange = Mathf.Lerp(row.ShownChange, row.TargetChange, ease);
                row.Base.anchorMax = new Vector2(row.ShownBase, 1f);
                row.Change.anchorMin = new Vector2(row.ShownBase, 0f);
                row.Change.anchorMax = new Vector2(row.ShownChange, 1f);
            }

            // Install flashes the button cyan and fades back.
            _installFlash = Mathf.Max(0f, _installFlash - dt * 1.5f);
            bool canInstall = _preview != Upgrades.GetTier(_vehicle, Categories[_selected]);
            Color buttonColor = canInstall ? Pink : new Color(1f, 1f, 1f, 0.15f);
            _installButton.color = Color.Lerp(buttonColor, Cyan, _installFlash);
        }

        private static void SnapStatBars()
        {
            foreach (StatRow row in _stats)
            {
                row.ShownBase = row.TargetBase;
                row.ShownChange = row.TargetChange;
            }
        }

        // ---------------------------------------------------------------- building the UI

        private static bool Build()
        {
            // I borrow the font from the game's speedometer so the menu matches the rest of the UI.
            if (!Singleton<VehicleCanvas>.InstanceExists || Singleton<VehicleCanvas>.Instance.SpeedText == null)
                return false;
            _font = Singleton<VehicleCanvas>.Instance.SpeedText.font;

            _root = new GameObject("SokudoTuningMenu");
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 500;
            // The game has its own class called CanvasScaler, so I name Unity's in full.
            UnityEngine.UI.CanvasScaler scaler = _root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _root.AddComponent<GraphicRaycaster>();
            _group = _root.AddComponent<CanvasGroup>();

            // My own entry in the game's state stack. These properties free the mouse, hide the game HUD and
            // compass, and lock walking and looking. CameraMode must stay Vehicle or the camera leaves the car.
            _state = _root.AddComponent<MonoState>();
            _state.Properties = new StateProperties(
                StateProperties.EMouseState.Free,
                StateProperties.ECrosshairState.Hidden,
                StateProperties.EEquippingState.Disabled,
                StateProperties.EInventoryState.Disabled,
                StateProperties.EHUDState.Hidden,
                StateProperties.ECompassState.Hidden,
                StateProperties.EMovementState.Locked,
                StateProperties.ELookState.Locked,
                StateProperties.EBlurState.Disabled,
                PlayerCamera.ECameraMode.Vehicle);
            _state.InitializeDefaultParent(SceneState.Current.InGame);
            _state.OnRemovedFromStack += OnClosed;

            RectTransform root = (RectTransform)_root.transform;

            // Dark gradient over the left of the screen so the text reads, fading out before it reaches the car.
            RectTransform shade = Stretch(root, "Shade", new Vector2(0f, 0f), new Vector2(0.62f, 1f));
            Image shadeImage = shade.gameObject.AddComponent<Image>();
            shadeImage.sprite = GradientSprite();
            shadeImage.color = Backdrop;
            shadeImage.raycastTarget = false;

            _content = Node(root, "Content", Vector2.zero, new Vector2(700f, 1080f));

            BuildHeader();
            BuildTiles();
            BuildDetail();
            BuildPromptBar(root);

            _canvas.enabled = false;
            return true;
        }

        private static void BuildHeader()
        {
            AddImage(_content, null, new Vector2(Left - 12f, -66f), new Vector2(6f, 128f), Pink);
            TextMeshProUGUI tag = AddText(_content, "SOKUDO TUNERS", 22f, new Vector2(Left + 6f, -60f), new Vector2(500f, 30f), Pink);
            tag.characterSpacing = 14f;
            AddText(_content, "PERFORMANCE", 72f, new Vector2(Left, -84f), new Vector2(640f, 84f), Color.white);
            _carName = AddText(_content, "", 26f, new Vector2(Left + 6f, -166f), new Vector2(500f, 34f), Cyan);
            _carName.characterSpacing = 8f;
        }

        private static void BuildTiles()
        {
            _tiles.Clear();
            Sprite tileSprite = SlantSprite(440, 70, 22);
            Sprite pipSprite = SlantSprite(20, 10, 4);

            for (int i = 0; i < Categories.Length; i++)
            {
                Image background = AddImage(_content, tileSprite, new Vector2(Left, -(TileTop + i * TileStep)), new Vector2(440f, 70f), TileIdle);
                background.raycastTarget = true;
                int index = i;
                MakeButton(background.gameObject, () => Select(index));

                RectTransform rect = background.rectTransform;
                Tile tile = new Tile
                {
                    Rect = rect,
                    Background = background,
                    Number = AddText(rect, (i + 1).ToString("00"), 18f, new Vector2(34f, -8f), new Vector2(40f, 22f), Dim),
                    Name = AddText(rect, Categories[i].ToString().ToUpperInvariant(), 32f, new Vector2(30f, -26f), new Vector2(280f, 40f), Color.white),
                    Pips = new Image[Upgrades.MaxTier]
                };

                // Little installed-tier pips on the right of each tile, so the whole build reads at a glance.
                for (int p = 0; p < Upgrades.MaxTier; p++)
                    tile.Pips[p] = AddImage(rect, pipSprite, new Vector2(330f + p * 26f, -32f), new Vector2(20f, 10f), Empty);

                _tiles.Add(tile);
            }
        }

        private static void BuildDetail()
        {
            Sprite pipSprite = SlantSprite(100, 16, 6);
            Sprite buttonSprite = SlantSprite(300, 54, 16);

            _categoryTitle = AddText(_content, "", 16f, new Vector2(Left, -DetailTop), new Vector2(200f, 22f), Dim);
            _categoryTitle.characterSpacing = 10f;
            _categoryBlurb = AddText(_content, "", 16f, new Vector2(Left + 170f, -DetailTop), new Vector2(330f, 22f), Dim);
            _categoryBlurb.fontStyle = FontStyles.Italic;

            _tierName = AddText(_content, "", 46f, new Vector2(Left - 2f, -(DetailTop + 22f)), new Vector2(300f, 54f), Cyan);
            _tierState = AddText(_content, "", 18f, new Vector2(Left + 230f, -(DetailTop + 44f)), new Vector2(260f, 24f), Color.white);
            _tierState.alignment = TextAlignmentOptions.Right;

            for (int t = 0; t <= Upgrades.MaxTier; t++)
            {
                Vector2 position = new Vector2(Left + t * 124f, -(DetailTop + 92f));
                _tierPips[t] = AddImage(_content, pipSprite, position, new Vector2(112f, 16f), Empty);
                _tierPips[t].rectTransform.pivot = new Vector2(0f, 0.5f);
                _tierPips[t].raycastTarget = true;
                int tier = t;
                MakeButton(_tierPips[t].gameObject, () => SetPreview(tier));
                _tierPipLabels[t] = AddText(_content, Upgrades.TierName(t).ToUpperInvariant(), 14f,
                    position + new Vector2(2f, -14f), new Vector2(112f, 20f), Dim);
            }

            for (int s = 0; s < _stats.Length; s++)
            {
                float y = -(DetailTop + 136f + s * 32f);
                RectTransform row = Node(_content, "Stat", new Vector2(Left, y), new Vector2(500f, 24f));
                StatRow stat = new StatRow { Root = row.gameObject };
                stat.Label = AddText(row, "", 16f, new Vector2(0f, 0f), new Vector2(140f, 24f), Color.white);

                Image track = AddImage(row, null, new Vector2(140f, -8f), new Vector2(280f, 8f), Empty);
                stat.Base = Stretch(track.rectTransform, "Base", Vector2.zero, Vector2.one);
                AddImageTo(stat.Base, Color.white);
                stat.Change = Stretch(track.rectTransform, "Change", Vector2.zero, Vector2.one);
                stat.ChangeImage = AddImageTo(stat.Change, Pink);

                stat.Delta = AddText(row, "", 16f, new Vector2(430f, 0f), new Vector2(70f, 24f), Pink);
                _stats[s] = stat;
            }

            _description = AddText(_content, "", 15f, new Vector2(Left, -(DetailTop + 236f)), new Vector2(500f, 22f), Dim);
            _description.fontStyle = FontStyles.Italic;

            _installButton = AddImage(_content, buttonSprite, new Vector2(Left, -(DetailTop + 270f)), new Vector2(300f, 54f), Pink);
            _installButton.raycastTarget = true;
            MakeButton(_installButton.gameObject, Install);
            _installLabel = AddText(_installButton.rectTransform, "", 22f, Vector2.zero, new Vector2(300f, 54f), Color.white);
            _installLabel.alignment = TextAlignmentOptions.Center;
        }

        // FiveM-style key hints along the bottom of the screen.
        private static void BuildPromptBar(RectTransform root)
        {
            RectTransform bar = Stretch(root, "Prompts", new Vector2(0f, 0f), new Vector2(1f, 0f));
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 52f);
            AddImageTo(bar, new Color(0f, 0f, 0f, 0.75f));
            AddImage(bar, null, new Vector2(0f, 0f), new Vector2(4000f, 2f), new Color(Pink.r, Pink.g, Pink.b, 0.6f));

            Sprite chipSprite = SlantSprite(64, 30, 6);
            float x = Left;
            x = AddPrompt(bar, chipSprite, x, "W/S", "PART");
            x = AddPrompt(bar, chipSprite, x, "A/D", "TIER");
            x = AddPrompt(bar, chipSprite, x, "ENTER", "INSTALL");
            x = AddPrompt(bar, chipSprite, x, "ESC", "EXIT");
            // Units goes last because its text changes width, and nothing sits after it to get pushed around.
            AddPrompt(bar, chipSprite, x, "U", "", out _unitsLabel);
        }

        private static float AddPrompt(RectTransform bar, Sprite chip, float x, string key, string action)
        {
            return AddPrompt(bar, chip, x, key, action, out _);
        }

        private static float AddPrompt(RectTransform bar, Sprite chip, float x, string key, string action, out TextMeshProUGUI actionText)
        {
            TextMeshProUGUI keyText = AddText(bar, key, 16f, Vector2.zero, new Vector2(200f, 30f), Color.white);
            float chipWidth = keyText.GetPreferredValues(key).x + 26f;
            Image chipImage = AddImage(bar, chip, new Vector2(x, -11f), new Vector2(chipWidth, 30f), new Color(1f, 1f, 1f, 0.16f));
            keyText.rectTransform.SetParent(chipImage.rectTransform, false);
            keyText.rectTransform.anchoredPosition = Vector2.zero;
            keyText.rectTransform.sizeDelta = new Vector2(chipWidth, 30f);
            keyText.alignment = TextAlignmentOptions.Center;

            actionText = AddText(bar, action, 16f, new Vector2(x + chipWidth + 10f, -14f), new Vector2(200f, 24f), Dim);
            return x + chipWidth + 10f + actionText.GetPreferredValues(action).x + 34f;
        }

        // ---------------------------------------------------------------- small UI helpers

        // Everything in the menu is positioned from the top-left, like reading a page.
        private static RectTransform Node(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Stretch(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static Image AddImage(RectTransform parent, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            Image image = AddImageTo(Node(parent, "Image", position, size), color);
            image.sprite = sprite;
            return image;
        }

        private static Image AddImageTo(RectTransform rect, Color color)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            // Only the things I turn into buttons should catch the mouse.
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI AddText(RectTransform parent, string value, float fontSize, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = Node(parent, "Text", position, size);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold | FontStyles.Italic;
            text.alignment = TextAlignmentOptions.Left;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.color = color;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        // A click target with no built-in hover tint or keyboard navigation. I drive the visuals myself.
        private static void MakeButton(GameObject go, UnityEngine.Events.UnityAction onClick)
        {
            Button button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);
        }

        private static void Notify(string title, string subtitle)
        {
            if (!Singleton<NotificationsManager>.InstanceExists)
                return;
            if (_notifyIcon == null)
                _notifyIcon = MakeSlantSprite(64, 64, 14, new Color32(255, 41, 133, 255));
            Singleton<NotificationsManager>.Instance.SendNotification(title, subtitle, _notifyIcon, 3f);
        }

        // ---------------------------------------------------------------- textures drawn in code

        // The slanted parallelogram behind tiles, pips, chips and the install button. That forward lean is most
        // of what makes it read as a racing game. Cached per size since several things share one.
        private static Sprite SlantSprite(int width, int height, int slant)
        {
            string key = $"{width}x{height}/{slant}";
            if (!_sprites.TryGetValue(key, out Sprite sprite) || sprite == null)
            {
                sprite = MakeSlantSprite(width, height, slant, new Color32(255, 255, 255, 255));
                _sprites[key] = sprite;
            }
            return sprite;
        }

        // Leans right like italic text: the top edge is shifted right by `slant` pixels. Edges get one pixel
        // of soft alpha so they don't look jagged.
        private static Sprite MakeSlantSprite(int width, int height, int slant, Color32 color)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float shift = slant * (y + 0.5f) / height;
                float leftEdge = shift;
                float rightEdge = width - slant + shift;
                for (int x = 0; x < width; x++)
                {
                    float centre = x + 0.5f;
                    float alpha = Mathf.Clamp01(centre - leftEdge + 0.5f) * Mathf.Clamp01(rightEdge - centre + 0.5f);
                    pixels[y * width + x] = new Color32(color.r, color.g, color.b, (byte)(alpha * color.a));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        // Solid on the left half, fading to clear on the right.
        private static Sprite GradientSprite()
        {
            if (_sprites.TryGetValue("gradient", out Sprite cached) && cached != null)
                return cached;

            const int width = 256;
            Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[width];
            for (int x = 0; x < width; x++)
            {
                float u = x / (width - 1f);
                float alpha = 0.92f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, u)));
                pixels[x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, 1f), new Vector2(0.5f, 0.5f), 100f);
            _sprites["gradient"] = sprite;
            return sprite;
        }
    }
}
