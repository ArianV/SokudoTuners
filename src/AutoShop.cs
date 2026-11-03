using System.Collections;
using System.Linq;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.Map;
using ScheduleOne.NPCs;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.NPCs.Schedules;
using ScheduleOne.UI;
using ScheduleOne.Vehicles;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SokudoTuners
{
    // Marco's auto shop at the downtown gas station is Sokudo Tuners. His bay detector tells me which car is parked
    // inside, and the paint shop's car spot is where I line the car up for the garage.
    public static class AutoShop
    {
        private const string PinName = "Sokudo Tuners";
        private const string HoursSignPath = "Map/Hyland Point/Region_Downtown/Gas Station/gas station/Bodyshop/OpeningHoursSign/Name";

        private static Marco _marco;
        private static POI _pin;
        private static float _nextSearch;

        public static VehicleDetector Bay => _marco != null ? _marco.VehicleDetector : null;

        public static Transform CarSpot =>
            Singleton<VehicleModMenu>.InstanceExists ? Singleton<VehicleModMenu>.Instance.VehiclePosition : null;

        public static bool IsInBay(LandVehicle vehicle) => vehicle != null && Bay != null && Bay.vehicles.Contains(vehicle);

        public static void Update()
        {
            if (_marco != null && _pin == null && Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 2f;
                AddMapPin();
            }

            if (_marco != null || Time.unscaledTime < _nextSearch)
                return;
            _nextSearch = Time.unscaledTime + 2f;

            // Marco may be switched off when he's far away, which FindObjectOfType skips.
            foreach (Marco marco in Resources.FindObjectsOfTypeAll<Marco>())
            {
                if (!marco.gameObject.scene.IsValid())
                    continue;
                _marco = marco;
                AddDialogueChoice(marco);
                OpenAllHours(marco);
                AddMapPin();
                ModSettings.LogDebug("Found Marco, Sokudo Tuners is open.");
                break;
            }
        }

        private static void AddDialogueChoice(Marco marco)
        {
            DialogueController controller = marco.DialogueHandler.GetComponent<DialogueController>();
            DialogueController.DialogueChoice choice = new DialogueController.DialogueChoice
            {
                ChoiceText = "I want to tune my car",
                Enabled = true,
                isValidCheck = CanTuneParkedCar
            };
            choice.onChoosen.AddListener(() => MelonCoroutines.Start(OpenForParkedCar()));
            controller.Choices.Insert(0, choice);
        }

        // His shift normally ends at 6PM. I stretch it over the whole day and switch his other plans off. Turning off the
        // action objects themselves survives the curfew, which only toggles the groups they sit in.
        private static void OpenAllHours(Marco marco)
        {
            NPCScheduleManager schedule = marco.GetComponentInChildren<NPCScheduleManager>(true);
            if (schedule == null)
                return;

            NPCAction[] actions = schedule.GetComponentsInChildren<NPCAction>(true);
            NPCEvent work = actions.OfType<NPCEvent_LocationBasedAction>().FirstOrDefault(a => a.GetName().Contains("Work"));
            if (work == null)
            {
                ModSettings.LogDebug("Couldn't find Marco's work shift, leaving his schedule alone.");
                return;
            }

            foreach (NPCAction action in actions)
                if (action != work)
                    action.gameObject.SetActive(false);

            work.StartTime = 0;
            work.Duration = 1439;
            work.EndTime = 2359;
            work.gameObject.SetActive(true);

            GameObject hours = GameObject.Find(HoursSignPath);
            if (hours != null && hours.TryGetComponent(out TMP_Text text))
                text.text = "24/7";
        }

        private static bool CanTuneParkedCar(out string reason)
        {
            LandVehicle car = Bay != null ? Bay.closestVehicle : null;
            if (car == null)
                reason = "Park your car inside the shop";
            else if (car.IsOccupied)
                reason = "Someone is in the vehicle";
            else if (!car.IsPlayerOwned)
                reason = "That's not your car";
            else
            {
                reason = string.Empty;
                return true;
            }
            return false;
        }

        // Same as the paint shop: fade out, line the car up, open, fade back in. The pause also lets Marco's dialogue close first.
        private static IEnumerator OpenForParkedCar()
        {
            LandVehicle car = Bay != null ? Bay.closestVehicle : null;
            if (car == null)
                yield break;

            Singleton<BlackOverlay>.Instance.Open(0.3f);
            yield return new WaitForSeconds(0.4f);

            if (CarSpot != null)
                car.AlignTo(CarSpot, EParkingAlignment.RearToKerb, network: true);
            TuningMenu.OpenOnFoot(car);

            Singleton<BlackOverlay>.Instance.Close(0.3f);
        }

        // There's no map pin for the shop, so I clone a plain one from the game and give it my name and icon.
        private static void AddMapPin()
        {
            if (_pin != null || Bay == null)
                return;

            // Lots of pins are switched off until needed (dead drops, customers), and a copy of one stays off too.
            // Business and property pins are always on and look like places, so I prefer those.
            POI template = Object.FindObjectsOfType<POI>()
                .Where(p => p.GetType() == typeof(POI) && p.enabled && p.UI != null && !p.Rotate)
                .OrderByDescending(p => p.transform.root.name.StartsWith("@Businesses") || p.transform.root.name.StartsWith("@Properties"))
                .FirstOrDefault();
            if (template == null)
            {
                ModSettings.LogDebug("No map pin to copy yet, trying again later.");
                return;
            }

            GameObject pin = Object.Instantiate(template.gameObject, Bay.transform.position, Quaternion.identity);
            pin.name = PinName;
            _pin = pin.GetComponent<POI>();
            _pin.enabled = true;
            ModSettings.LogDebug($"Map pin copied from '{template.MainText}'.");
            _pin.MainTextVisibility = POI.TextShowMode.Always;
            _pin.SetMainText(PinName);
            _pin.onUICreated.AddListener(SetPinIcon);
            if (_pin.UISetup)
                SetPinIcon();
        }

        private static void SetPinIcon()
        {
            if (_pin == null || _pin.IconContainer == null || ModAssets.PinGlyph == null)
                return;

            // Keep the pin's own round background but tint it pink, and swap the symbol on top for my S.
            Image[] images = _pin.IconContainer.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (i == 0 && images.Length > 1)
                {
                    images[i].color = new Color(1f, 0.16f, 0.52f);
                    continue;
                }
                images[i].sprite = ModAssets.PinGlyph;
                images[i].color = Color.white;
                images[i].preserveAspect = true;
            }
        }
    }
}
