using HarmonyLib;
using ScheduleOne.DevUtilities;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // Orbits the car while the garage is open. VehicleCamera moves the camera itself every LateUpdate, so I skip it while the menu is up.
    [HarmonyPatch(typeof(VehicleCamera), "LateUpdate")]
    public static class GarageCamera
    {
        private const float OrbitDegreesPerSecond = 12f;
        private const float HeightAboveCentre = 1.4f;

        private const float AimOffset = 0.28f;

        private static LandVehicle _vehicle;
        private static bool _onFoot;
        private static float _angle;

        private static bool Prefix() => !TuningMenu.IsOpen && !ShopCutscene.Playing;

        public static void Begin(LandVehicle vehicle, bool onFoot)
        {
            _vehicle = vehicle;
            _onFoot = onFoot;
            if (!PlayerSingleton<PlayerCamera>.InstanceExists)
                return;

            PlayerCamera camera = PlayerSingleton<PlayerCamera>.Instance;
            // On foot the camera hangs off the player. Overriding it unhooks it, like the paint shop does.
            if (onFoot)
                camera.OverrideTransform(camera.transform.position, camera.transform.rotation, 0f);

            Vector3 offset = camera.transform.position - Centre(vehicle);
            _angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        }

        public static void End()
        {
            if (_onFoot && PlayerSingleton<PlayerCamera>.InstanceExists)
                PlayerSingleton<PlayerCamera>.Instance.StopTransformOverride(0f);
            _vehicle = null;
            _onFoot = false;
        }

        public static void LateUpdate()
        {
            if (!TuningMenu.IsOpen || _vehicle == null || !PlayerSingleton<PlayerCamera>.InstanceExists)
                return;

            Transform camera = PlayerSingleton<PlayerCamera>.Instance.transform;
            float dt = Time.unscaledDeltaTime;
            _angle += OrbitDegreesPerSecond * dt;

            Vector3 centre = Centre(_vehicle);
            float distance = Mathf.Clamp(_vehicle.BoundingBoxDimensions.magnitude * 1.15f, 5f, 12f);
            Vector3 direction = Quaternion.Euler(0f, _angle, 0f) * Vector3.forward;
            Vector3 target = KeepOutOfWalls(_vehicle, centre, centre + direction * distance + Vector3.up * HeightAboveCentre);

            float ease = 1f - Mathf.Exp(-3f * dt);
            camera.position = Vector3.Lerp(camera.position, target, ease);

            Vector3 toCar = (centre - camera.position).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, toCar);
            Vector3 aimPoint = centre - right * distance * AimOffset;
            camera.rotation = Quaternion.Slerp(camera.rotation, Quaternion.LookRotation(aimPoint - camera.position), ease);
        }

        private static Vector3 Centre(LandVehicle vehicle) => vehicle.transform.position + Vector3.up * 0.9f;

        // Keeps the camera from swinging through walls when I'm parked next to a building or inside the shop.
        // Rays don't hit the back of a mesh, so from inside a room they pass straight out through the wall.
        // Casting back from the camera's spot as well catches the wall's outside face.
        public static Vector3 KeepOutOfWalls(LandVehicle vehicle, Vector3 centre, Vector3 wanted)
        {
            Vector3 toCamera = wanted - centre;
            float distance = toCamera.magnitude;
            Vector3 direction = toCamera / distance;

            float allowed = distance;
            foreach (RaycastHit hit in Physics.RaycastAll(centre, direction, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!Ignored(vehicle, hit.collider))
                    allowed = Mathf.Min(allowed, hit.distance - 0.3f);
            foreach (RaycastHit hit in Physics.RaycastAll(wanted, -direction, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!Ignored(vehicle, hit.collider))
                    allowed = Mathf.Min(allowed, distance - hit.distance - 0.3f);

            return centre + direction * Mathf.Max(allowed, 1.5f);
        }

        private static bool Ignored(LandVehicle vehicle, Collider collider)
        {
            Transform hit = collider.transform;
            return (vehicle != null && hit.IsChildOf(vehicle.transform)) || (Player.Local != null && hit.IsChildOf(Player.Local.transform));
        }
    }
}
