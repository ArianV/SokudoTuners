using HarmonyLib;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // Tire upgrade. The game rewrites each wheel's grip curves every physics step in ApplyFriction (rain and
    // handbrake included), so setting grip once wouldn't stick. I scale the stiffness right after it runs,
    // which keeps the rain and handbrake drift behaviour, just with grippier rubber.
    [HarmonyPatch(typeof(Wheel), "ApplyFriction")]
    public static class TirePatch
    {
        private static readonly AccessTools.FieldRef<Wheel, LandVehicle> Vehicle =
            AccessTools.FieldRefAccess<Wheel, LandVehicle>("vehicle");

        private static void Postfix(Wheel __instance)
        {
            LandVehicle vehicle = Vehicle(__instance);
            if (vehicle == null || Upgrades.GetTier(vehicle, UpgradeType.Tires) == 0)
                return;

            // WheelFrictionCurve is a struct, so I get a copy, change it, and assign it back.
            // Changing the copy alone would do nothing.
            WheelCollider wheel = __instance.wheelCollider;

            WheelFrictionCurve forward = wheel.forwardFriction;
            forward.stiffness *= Upgrades.TireForwardGripMultiplier(vehicle);
            wheel.forwardFriction = forward;

            WheelFrictionCurve sideways = wheel.sidewaysFriction;
            sideways.stiffness *= Upgrades.TireSideGripMultiplier(vehicle);
            wheel.sidewaysFriction = sideways;
        }
    }
}
