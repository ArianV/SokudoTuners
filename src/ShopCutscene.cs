using System;
using System.Collections;
using HarmonyLib;
using MelonLoader;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.State;
using ScheduleOne.UI;
using ScheduleOne.Vehicles;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SokudoTuners
{
    // Drive into the shop and it takes over: fade out, the car rolls into the bay nose first while a camera watches it
    // pull in, then the garage opens. Space, Enter or Esc skips it.
    //
    // I glide the car along the path myself instead of driving it. While the game thinks a car shouldn't be physically
    // simulated it turns the physics off and spins the wheels from how far the car moved, so I just say no for this car.
    public static class ShopCutscene
    {
        private const float RollDistance = 6f;
        private const float RollSeconds = 3.2f;
        private const float OutDistance = 10f;
        private const float OutSeconds = 3.2f;
        private const float StraightBeforeTurning = 5f;
        private const float ExitCheckDistance = 12f;

        public static bool Playing { get; private set; }

        private static LandVehicle _controlled;
        private static bool _wasInBay;
        private static bool _armed;
        private static float _cooldownUntil;
        private static bool _skip;
        private static bool _subscribed;

        private static GameObject _stateObject;
        private static MonoState _state;
        private static GameInput.ExitDelegate _exitListener;

        private static bool _shotActive;
        private static Vector3 _shotPosition;

        public static bool Controls(LandVehicle vehicle) => _controlled != null && vehicle == _controlled;

        public static void Update()
        {
            if (!_subscribed)
            {
                TuningMenu.Closed += OnMenuClosed;
                _subscribed = true;
            }

            // Entering the bay arms it, and it fires once I'm heading in. Checking only the frame I crossed the line
            // missed anyone who came in a bit quick.
            LandVehicle vehicle = LocalDriver.GetVehicle();
            bool inBay = AutoShop.IsInBay(vehicle);
            if (inBay && !_wasInBay)
                _armed = true;
            if (!inBay)
                _armed = false;
            _wasInBay = inBay;

            if (_armed && !Playing && !TuningMenu.IsOpen && Time.unscaledTime > _cooldownUntil && DrivingIn(vehicle))
            {
                _armed = false;
                MelonCoroutines.Start(DriveIn(vehicle));
            }

            Keyboard keyboard = Keyboard.current;
            if (Playing && keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                _skip = true;
        }

        // Heading into the shop, so reversing out doesn't set it off. The cutscene puts the car where it wants it anyway,
        // so speed only matters for not flying straight through the back wall first.
        private static bool DrivingIn(LandVehicle vehicle)
        {
            Transform spot = AutoShop.CarSpot;
            if (spot == null)
                return false;
            Vector3 velocity = vehicle.Rb.velocity;
            float speed = ModSettings.DisplaySpeed(velocity.magnitude);
            return speed < (ModSettings.UseMph ? 60f : 100f) && Vector3.Dot(velocity, -Flat(spot.forward)) > 0.3f;
        }

        private static IEnumerator DriveIn(LandVehicle car)
        {
            Playing = true;
            _skip = false;
            PushState();

            // FrontToKerb on the paint shop's spot parks the car nose in, in the same footprint as the paint shop uses.
            Transform spot = AutoShop.CarSpot;
            Tuple<Vector3, Quaternion> end = car.GetAlignmentTransform(spot, EParkingAlignment.FrontToKerb);
            Vector3 outward = Flat(spot.forward);
            Vector3 start = end.Item1 + outward * RollDistance;

            // Physics off straight away, and the car coasts to a stop on its own momentum while the screen fades, so
            // coming in quick doesn't end with a crash into the back wall before it goes black.
            Vector3 carry = car.Rb.velocity;
            _controlled = car;
            Singleton<BlackOverlay>.Instance.Open(0.25f);
            for (float t = 0f; t < 0.3f && car != null; t += Time.deltaTime)
            {
                car.transform.position += carry * Time.deltaTime;
                carry *= Mathf.Exp(-6f * Time.deltaTime);
                yield return null;
            }
            if (car == null)
            {
                _controlled = null;
                PopState();
                Playing = false;
                yield break;
            }

            car.transform.SetPositionAndRotation(start, end.Item2);
            StartShot(car, end.Item1, outward);
            Singleton<BlackOverlay>.Instance.Close(0.4f);

            for (float t = 0f; t < RollSeconds && !_skip && car != null; t += Time.deltaTime)
            {
                float eased = 1f - Mathf.Pow(1f - t / RollSeconds, 3f);
                car.transform.position = Vector3.Lerp(start, end.Item1, eased);
                yield return null;
            }

            if (car != null)
            {
                car.transform.SetPositionAndRotation(end.Item1, end.Item2);
                TuningMenu.OpenFromCutscene(car);
            }
            _shotActive = false;
            PopState();
            Playing = false;
        }

        // Closing the garage while sat in the car at the shop drives it out. On foot (Marco) it just stays parked.
        private static void OnMenuClosed(LandVehicle car, bool onFoot)
        {
            _cooldownUntil = Time.unscaledTime + 3f;
            if (car != null && !onFoot && (Controls(car) || AutoShop.IsInBay(car)) && AutoShop.CarSpot != null)
                MelonCoroutines.Start(DriveOut(car));
            else
                _controlled = null;
        }

        // Fade, car nose-out in the bay, then it pulls out through the door past a camera outside, picking up speed.
        // I let go while it's still rolling, and the game hands the physics back with the speed it was doing.
        private static IEnumerator DriveOut(LandVehicle car)
        {
            Playing = true;
            _skip = false;
            PushState();
            _controlled = car;

            Singleton<BlackOverlay>.Instance.Open(0.25f);
            yield return new WaitForSeconds(0.3f);
            if (car == null)
            {
                Finish();
                yield break;
            }

            Transform spot = AutoShop.CarSpot;
            Tuple<Vector3, Quaternion> park = car.GetAlignmentTransform(spot, EParkingAlignment.RearToKerb);
            Vector3 outward = Flat(spot.forward);
            Vector3 start = park.Item1;
            float turn = ClearTurn(car, start, outward);

            car.transform.SetPositionAndRotation(start, park.Item2);
            StartOutsideShot(car, start, outward);
            Singleton<BlackOverlay>.Instance.Close(0.4f);

            // Distance grows with time squared, so it pulls away from a standstill and is moving at 2D/T when I let go.
            for (float t = 0f; t < OutSeconds && !_skip && car != null; t += Time.deltaTime)
            {
                float k = t / OutSeconds;
                PlaceOnExitPath(car, start, outward, turn, OutDistance * k * k);
                yield return null;
            }

            if (car != null && _skip)
                PlaceOnExitPath(car, start, outward, turn, OutDistance);
            Finish();
        }

        // Straight out until the car is through the door, then an even curve onto the clear heading. Walked in small
        // steps rather than solved, it's only a few metres.
        private static void PlaceOnExitPath(LandVehicle car, Vector3 start, Vector3 outward, float turn, float distance)
        {
            const int steps = 40;
            float step = distance / steps;
            Vector3 position = start;
            Vector3 heading = outward;
            for (int i = 0; i < steps; i++)
            {
                float travelled = step * i;
                float bend = Mathf.Clamp01((travelled - StraightBeforeTurning) / (OutDistance - StraightBeforeTurning));
                heading = Quaternion.AngleAxis(turn * bend, Vector3.up) * outward;
                position += heading * step;
            }
            car.transform.SetPositionAndRotation(position, Quaternion.LookRotation(heading, Vector3.up));
        }

        // Things like the payphone outside the door are baked into the scenery, so I can't move them out of the way.
        // Instead I sweep a car-sized box out of the door and take the straightest heading that's clear.
        private static float ClearTurn(LandVehicle car, Vector3 start, Vector3 outward)
        {
            Vector3 from = start + outward * StraightBeforeTurning + Vector3.up * 0.9f;
            Vector3 halfSize = new Vector3(1.1f, 0.5f, 0.3f);
            float bestAngle = 0f;
            float bestRoom = -1f;

            foreach (float angle in new[] { 0f, 12f, -12f, 22f, -22f, 32f, -32f })
            {
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * outward;
                float room = ExitCheckDistance;
                foreach (RaycastHit hit in Physics.BoxCastAll(from, halfSize, direction, Quaternion.LookRotation(direction), ExitCheckDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    Transform other = hit.collider.transform;
                    if (other.IsChildOf(car.transform) || (Player.Local != null && other.IsChildOf(Player.Local.transform)))
                        continue;
                    room = Mathf.Min(room, hit.distance);
                }

                if (room >= ExitCheckDistance)
                    return angle;
                if (room > bestRoom)
                {
                    bestRoom = room;
                    bestAngle = angle;
                }
            }
            return bestAngle;
        }

        private static void Finish()
        {
            _controlled = null;
            _shotActive = false;
            PopState();
            Playing = false;
        }

        private static void StartOutsideShot(LandVehicle car, Vector3 start, Vector3 outward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, outward);
            Vector3 outside = start + outward * (OutDistance + 1.5f) + Vector3.up * 1.2f;
            float side = Room(car, outside, right) >= Room(car, outside, -right) ? 1f : -1f;
            _shotPosition = GarageCamera.KeepOutOfWalls(car, outside, outside + right * side * 3.5f);
            _shotActive = true;

            if (PlayerSingleton<PlayerCamera>.InstanceExists)
            {
                Transform camera = PlayerSingleton<PlayerCamera>.Instance.transform;
                camera.position = _shotPosition;
                camera.rotation = Quaternion.LookRotation(car.transform.position + Vector3.up * 0.6f - _shotPosition);
            }
        }

        // A low camera off to whichever side of the bay has more room, watching the car pull in.
        private static void StartShot(LandVehicle car, Vector3 end, Vector3 outward)
        {
            Vector3 right = Vector3.Cross(Vector3.up, outward);
            Vector3 eye = end + Vector3.up * 0.9f;
            float side = Room(car, eye, right) >= Room(car, eye, -right) ? 1f : -1f;
            _shotPosition = GarageCamera.KeepOutOfWalls(car, eye, end + right * side * 3.2f + outward * 1.2f + Vector3.up * 1.3f);
            _shotActive = true;

            if (PlayerSingleton<PlayerCamera>.InstanceExists)
            {
                Transform camera = PlayerSingleton<PlayerCamera>.Instance.transform;
                camera.position = _shotPosition;
                camera.rotation = Quaternion.LookRotation(car.transform.position + Vector3.up * 0.6f - _shotPosition);
            }
        }

        public static void LateUpdate()
        {
            if (!_shotActive || _controlled == null || !PlayerSingleton<PlayerCamera>.InstanceExists)
                return;
            Transform camera = PlayerSingleton<PlayerCamera>.Instance.transform;
            camera.position = _shotPosition;
            Quaternion look = Quaternion.LookRotation(_controlled.transform.position + Vector3.up * 0.6f - _shotPosition);
            camera.rotation = Quaternion.Slerp(camera.rotation, look, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        private static float Room(LandVehicle car, Vector3 from, Vector3 direction)
        {
            Vector3 to = GarageCamera.KeepOutOfWalls(car, from, from + direction * 4f);
            return Vector3.Distance(from, to);
        }

        // My own entry in the game's state stack, so the car ignores input and the HUD hides while it plays.
        private static void PushState()
        {
            if (_stateObject == null)
            {
                _stateObject = new GameObject("SokudoCutscene");
                _state = _stateObject.AddComponent<MonoState>();
                _state.InitializeDefaultParent(SceneState.Current.InGame);
                _state.Properties = new StateProperties(
                    StateProperties.EMouseState.Locked,
                    StateProperties.ECrosshairState.Hidden,
                    StateProperties.EEquippingState.Disabled,
                    StateProperties.EInventoryState.Disabled,
                    StateProperties.EHUDState.Hidden,
                    StateProperties.ECompassState.Hidden,
                    StateProperties.EMovementState.Locked,
                    StateProperties.ELookState.Locked,
                    StateProperties.EBlurState.Disabled,
                    PlayerCamera.ECameraMode.Vehicle);
            }
            _state.PushToDefaultParent();
            _exitListener = OnExit;
            GameInput.RegisterExitListener(_exitListener, 10);
        }

        private static void PopState()
        {
            if (_exitListener != null)
                GameInput.DeregisterExitListener(_exitListener);
            if (_state != null)
                _state.RemoveFromDefaultParent();
        }

        private static void OnExit(ExitAction action)
        {
            if (!action.Used && Playing)
            {
                action.Used = true;
                _skip = true;
            }
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.normalized;
        }
    }

    [HarmonyPatch(typeof(LandVehicle), "ShouldBePhysicallySimulated")]
    public static class CutsceneCarPatch
    {
        private static bool Prefix(LandVehicle __instance, ref bool __result)
        {
            if (!ShopCutscene.Controls(__instance))
                return true;
            __result = false;
            return false;
        }
    }
}
