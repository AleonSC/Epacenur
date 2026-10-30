using System.Collections.Generic;
using Epacenur.Core;
using UnityEngine;

namespace Epacenur.Runtime
{
    /// Plays the sound effects (Core: Synth makes them; there are no sound files). Sounds about you or the HUD
    /// play flat (Play); sounds out in the world come from where they happen (PlayAt), quieter further away.
    /// The volume (Volume, 0..1) is kept between sessions.
    public static class Sfx
    {
        const int Voices = 12;           // sounds that can play at once
        const float Hearing = 40f;       // world sounds further than this from the camera aren't played
        const string VolumeKey = "Epacenur.Volume";

        static readonly Dictionary<Sound, AudioClip> clips = new Dictionary<Sound, AudioClip>();
        static AudioSource[] voices;
        static int next;
        static float volume = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            clips.Clear();
            voices = null;
            next = 0;
            volume = -1f;
        }

        /// 0 (off) to 1.
        public static float Volume
        {
            get
            {
                if (volume < 0f)
                    volume = PlayerPrefs.HasKey(VolumeKey) && float.TryParse(PlayerPrefs.GetString(VolumeKey), out float v) ? Mathf.Clamp01(v) : 0.8f;
                return volume;
            }
            set
            {
                volume = Mathf.Clamp01(value);
                PlayerPrefs.SetString(VolumeKey, volume.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        /// Steps the volume: full, half, a quarter, off, and round again.
        public static void CycleVolume()
        {
            float v = Volume;
            Volume = v > 0.6f ? 0.4f : v > 0.25f ? 0.2f : v > 0f ? 0f : 0.8f;
        }

        public static string VolumeLabel => Volume <= 0f ? "Sound: off" : "Sound: " + Mathf.RoundToInt(Volume / 0.8f * 100f) + "%";

        /// A sound heard flat (yours, or the HUD's), a little varied in pitch so repeats don't drone.
        public static void Play(Sound sound, float loudness = 1f, float vary = 0.05f) => Start(sound, null, loudness, vary);

        /// A sound from a point in the world (world space).
        public static void PlayAt(Sound sound, Vector3 at, float loudness = 1f, float vary = 0.06f) => Start(sound, at, loudness, vary);

        static void Start(Sound sound, Vector3? at, float loudness, float vary)
        {
            if (Volume <= 0f)
                return;
            var cam = Camera.main;
            if (at.HasValue && cam != null && (at.Value - cam.transform.position).magnitude > Hearing)
                return;
            if (voices == null)
                MakeVoices();
            var v = voices[next];
            next = (next + 1) % voices.Length;
            v.Stop();
            v.clip = Clip(sound);
            v.volume = Volume * loudness;
            v.pitch = 1f + Random.Range(-vary, vary);
            v.spatialBlend = at.HasValue ? 0.85f : 0f;  // mostly from where it happens, not all (it stays clear)
            if (at.HasValue)
                v.transform.position = at.Value;
            else if (cam != null)
                v.transform.position = cam.transform.position;
            v.Play();
        }

        static void MakeVoices()
        {
            var host = new GameObject("Sound effects");
            Object.DontDestroyOnLoad(host);
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("Voice " + i);
                go.transform.SetParent(host.transform, false);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 4f;
                s.maxDistance = Hearing;
                s.dopplerLevel = 0f;
                voices[i] = s;
            }
        }

        /// A sound's clip (for something that plays it itself, like the rain's loop).
        public static AudioClip ClipFor(Sound sound) => Clip(sound);

        static AudioClip Clip(Sound sound)
        {
            if (clips.TryGetValue(sound, out var clip) && clip != null)
                return clip;
            var samples = Synth.Make(sound);
            clip = AudioClip.Create(sound.ToString(), samples.Length, 1, Synth.Rate, false);
            clip.SetData(samples, 0);
            clips[sound] = clip;
            return clip;
        }
    }
}
