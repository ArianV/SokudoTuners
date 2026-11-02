using HarmonyLib;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // Slowly circles the car while the garage is open, like the NFS garage.
    //
    // In a car the game's VehicleCamera places the camera itself every LateUpdate, so I switch that off while the
    // menu is open (a prefix returning false skips the original method) and place the camera myself instead.
    // When the menu closes, VehicleCamera picks up again and eases back behind the car on its own.
    [HarmonyPatch(typeof(VehicleCamera), "LateUpdate")]
    public static class GarageCamera
    {
        private const float OrbitDegreesPerSecond = 12f;
        private const float HeightAboveCentre = 1.4f;

        // How much of the orbit distance to aim off to the left, so the car sits in the open space right of the menu.
        private const float AimOffset = 0.28f;

        private static LandVehicle _vehicle;
        private static float _angle;

        private static bool Prefix() => !TuningMenu.IsOpen;

        // Called when the menu opens. I start the orbit from wherever the camera already is so there's no jump.
        public static void Begin(LandVehicle vehicle)
        {
            _vehicle = vehicle;
            if (!PlayerSingleton<PlayerCamera>.InstanceExists)
                return;

            Vector3 offset = PlayerSingleton<PlayerCamera>.Instance.transform.position - Centre(vehicle);
            _angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        }

        // Called every frame from the mod's OnLateUpdate, after the game has moved everything else.
        public static void LateUpdate()
        {
            if (!TuningMenu.IsOpen || _vehicle == null || !PlayerSingleton<PlayerCamera>.InstanceExists)
                return;

            Transform camera = PlayerSingleton<PlayerCamera>.Instance.transform;
            float dt = Time.unscaledDeltaTime;
            _angle += OrbitDegreesPerSecond * dt;

            // Bigger cars get a wider orbit so the whole thing stays in frame.
            Vector3 centre = Centre(_vehicle);
            float distance = Mathf.Clamp(_vehicle.BoundingBoxDimensions.magnitude * 1.15f, 5f, 12f);
            Vector3 direction = Quaternion.Euler(0f, _angle, 0f) * Vector3.forward;
            Vector3 target = KeepOutOfWalls(centre, centre + direction * distance + Vector3.up * HeightAboveCentre);

            // Ease rather than snap, which also turns the first second into a smooth swing out from the driving view.
            float ease = 1f - Mathf.Exp(-3f * dt);
            camera.position = Vector3.Lerp(camera.position, target, ease);

            Vector3 toCar = (centre - camera.position).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, toCar);
            Vector3 aimPoint = centre - right * distance * AimOffset;
            camera.rotation = Quaternion.Slerp(camera.rotation, Quaternion.LookRotation(aimPoint - camera.position), ease);
        }

        private static Vector3 Centre(LandVehicle vehicle) => vehicle.transform.position + Vector3.up * 0.9f;

        // Parked next to a building, the orbit would swing through the wall. I cast from the car out to where
        // the camera wants to be and pull it in front of the first thing in the way, ignoring the car's own colliders.
        private static Vector3 KeepOutOfWalls(Vector3 centre, Vector3 wanted)
        {
            Vector3 toCamera = wanted - centre;
            float distance = toCamera.magnitude;
            Vector3 direction = toCamera / distance;

            float allowed = distance;
            foreach (RaycastHit hit in Physics.RaycastAll(centre, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(_vehicle.transform))
                    continue;
                allowed = Mathf.Min(allowed, hit.distance - 0.3f);
            }

            return centre + direction * Mathf.Max(allowed, 1.5f);
        }
    }
}
