using HarmonyLib;
using ScheduleOne.Vehicles;
using UnityEngine;

namespace SokudoTuners
{
    // ApplyFriction rewrites grip every step (rain, handbrake), so I scale it right after.
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
