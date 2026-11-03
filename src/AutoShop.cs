using System.Collections;
using MelonLoader;
using ScheduleOne.DevUtilities;
using ScheduleOne.Dialogue;
using ScheduleOne.Map;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.UI;
using ScheduleOne.Vehicles;
using UnityEngine;
using UnityEngine.UI;

namespace SokudoTuners
{
    // Marco's auto shop at the downtown gas station is Sokudo Tuners. His bay detector tells me which car is parked
    // inside, and the paint shop's car spot is where I line the car up for the garage.
    public static class AutoShop
    {
        private const string PinName = "Sokudo Tuners";

        private static Marco _marco;
        private static POI _pin;
        private static float _nextSearch;

        public static VehicleDetector Bay => _marco != null ? _marco.VehicleDetector : null;

        public static Transform CarSpot =>
            Singleton<VehicleModMenu>.InstanceExists ? Singleton<VehicleModMenu>.Instance.VehiclePosition : null;

        public static bool IsInBay(LandVehicle vehicle) => vehicle != null && Bay != null && Bay.vehicles.Contains(vehicle);

        public static void Update()
        {
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

            POI template = null;
            foreach (POI poi in Object.FindObjectsOfType<POI>())
            {
                if (poi.GetType() == typeof(POI))
                {
                    template = poi;
                    break;
                }
            }
            if (template == null)
                return;

            GameObject pin = Object.Instantiate(template.gameObject, Bay.transform.position, Quaternion.identity);
            pin.name = PinName;
            _pin = pin.GetComponent<POI>();
            _pin.MainTextVisibility = POI.TextShowMode.Always;
            _pin.SetMainText(PinName);
            _pin.onUICreated.AddListener(SetPinIcon);
            if (_pin.UISetup)
                SetPinIcon();
        }

        private static void SetPinIcon()
        {
            if (_pin == null || _pin.IconContainer == null || ModAssets.Icon == null)
                return;
            foreach (Image image in _pin.IconContainer.GetComponentsInChildren<Image>(true))
            {
                image.sprite = ModAssets.Icon;
                image.color = Color.white;
            }
        }
    }
}
