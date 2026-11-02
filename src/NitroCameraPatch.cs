using HarmonyLib;
using ScheduleOne.PlayerScripts;
using UnityEngine;

namespace SokudoTuners
{
    // Widens the camera while nitro fires. I don't use the game's own FOV override system for this:
    // only the highest-priority override applies (so I'd fight drug effects), and the camera eases at
    // 4 degrees a second, which turns a punchy kick into a slow drift.
    //
    // Instead I add my kick after the game sets the FOV, and take it back out before the game runs
    // next frame. The game's own easing never sees my kick, so the two can't stack up.
    [HarmonyPatch(typeof(PlayerCamera), "LateUpdate")]
    public static class NitroCameraPatch
    {
        private const float BoostFovKick = 16f;

        // Snaps in fast, fades out slower, like the rush wearing off.
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

            // If something else set the FOV since my last frame (a cutscene, aiming a gun), it has
            // already replaced my value, so there's nothing of mine left to take out.
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
