using HarmonyLib;
using MelonLoader;
using ScheduleOne.Audio;
using ScheduleOne.Core.Audio;
using ScheduleOne.DevUtilities;
using ScheduleOne.UI.Polling;
using UnityEngine;

namespace SokudoTuners
{
    // Reuses the game's own UI sounds, so I ship no audio and the UI volume slider applies.
    public static class MenuSounds
    {
        private static readonly AccessTools.FieldRef<ButtonSound, AudioClip> HoverClip =
            AccessTools.FieldRefAccess<ButtonSound, AudioClip>("_hoverClip");
        private static readonly AccessTools.FieldRef<ButtonSound, AudioClip> ClickClip =
            AccessTools.FieldRefAccess<ButtonSound, AudioClip>("_clickClip");

        private static AudioSourceController _move;
        private static AudioSourceController _click;
        private static AudioSourceController _install;

        public static void Move(float pitch = 0.9f) => Play(_move, pitch);
        public static void Click() => Play(_click, 1f);

        public static void Install()
        {
            Play(_click, 1f);
            Play(_install, 1f);
        }

        public static void Prepare()
        {
            if (_move != null || !Singleton<AudioManager>.InstanceExists)
                return;

            AudioClip hover = null;
            AudioClip click = null;
            foreach (ButtonSound button in Resources.FindObjectsOfTypeAll<ButtonSound>())
            {
                if (hover == null) hover = HoverClip(button);
                if (click == null) click = ClickClip(button);
                if (hover != null && click != null)
                    break;
            }

            AudioClip success = null;
            foreach (PollPanel poll in Resources.FindObjectsOfTypeAll<PollPanel>())
            {
                if (poll.SubmissionSuccessSound != null)
                {
                    AudioSource source = poll.SubmissionSuccessSound.GetComponent<AudioSource>();
                    if (source != null && source.clip != null)
                    {
                        success = source.clip;
                        break;
                    }
                }
            }

            AudioManager audio = Singleton<AudioManager>.Instance;
            _move = Create(audio, hover, 0.6f);
            _click = Create(audio, click, 0.8f);
            _install = Create(audio, success ?? click, 1f);

            Melon<SokudoTunersMod>.Logger.Msg(
                $"Menu sounds: move '{Name(hover)}', click '{Name(click)}', install '{Name(success)}'");
        }

        private static AudioSourceController Create(AudioManager audio, AudioClip clip, float volume)
        {
            if (clip == null)
                return null;
            AudioSourceController controller = audio.CreateAudioSourceController(EAudioType.UI, clip, volume);
            controller.name = "SokudoMenuSound";
            return controller;
        }

        private static void Play(AudioSourceController controller, float pitch)
        {
            if (controller == null)
                return;
            controller.PitchMultiplier = pitch;
            controller.PlayOneShot();
        }

        private static string Name(AudioClip clip) => clip != null ? clip.name : "none found";
    }
}
