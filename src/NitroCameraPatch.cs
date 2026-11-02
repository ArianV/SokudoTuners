using HarmonyLib;
using ScheduleOne.PlayerScripts;
using UnityEngine;

namespace SokudoTuners
{
    // Not using the game's FoVChangeSmoother: only the top override applies and it eases at 4 deg/s, way too slow for a kick.
    // I add the kick after the game sets the FOV and take it back out before its next update so they never stack.
    [HarmonyPatch(typeof(PlayerCamera), "LateUpdate")]
    public static class NitroCameraPatch
    {
        private const float BoostFovKick = 16f;

        private const float KickInSpeed = 10f;
        private const float KickOutSpeed = 3f;

        private static float _kick;
        private static float _appliedKick;
        private static float _lastWrittenFov = -1f;

        private static void Prefix(PlayerCamera __instance)
        {
            Camera camera = __instance.Camera;
            if (camera == null || _appliedKick == 0f)
                return;

            // Something else (cutscene, aiming) already replaced the FOV.
            if (Mathf.Approximately(camera.fieldOfView, _lastWrittenFov))
                camera.fieldOfView -= _appliedKick;
            _appliedKick = 0f;
        }

        private static void Postfix(PlayerCamera __instance)
        {
            Camera camera = __instance.Camera;
            if (camera == null)
                return;

            bool boosting = LocalDriver.IsBoosting;
            float speed = boosting ? KickInSpeed : KickOutSpeed;
            _kick = Mathf.Lerp(_kick, boosting ? BoostFovKick : 0f, 1f - Mathf.Exp(-speed * Time.deltaTime));

            if (_kick < 0.01f)
            {
                _kick = 0f;
                return;
            }

            camera.fieldOfView += _kick;
            _appliedKick = _kick;
            _lastWrittenFov = camera.fieldOfView;
        }
    }
}
