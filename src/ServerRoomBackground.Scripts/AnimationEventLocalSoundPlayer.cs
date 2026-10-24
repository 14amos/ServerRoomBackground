using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Audio
{
    // Rebuilt from live EFT 1.2.5 scene data (class missing in SPT 0.16.9). Serialized layout = live's 516 bytes:
    // Sound[6] { "ButtonClick", "IncomingCall_01".."_05" }, an unset reference, AudioSource_01, AudioSource_02.
    // The radio's LCD animation sends PlaySoundBank("<name>") animation events; Unity calls them on every component of
    // the object (GenericEventTranslator there gets them too, with no listener in this scene).
    public class AnimationEventLocalSoundPlayer : MonoBehaviour
    {
        [Serializable]
        public class Sound
        {
            public string Name;
            public AudioClip Clip;
            public int SourceIndex;      // ButtonClick 0, IncomingCall_* 1
            public float Volume;         // ButtonClick 0.08, IncomingCall_* 1
            public float Pitch;          // all 1
            public float Unknown0;       // ButtonClick 0, IncomingCall_* 0.5 (meaning not known: unused)
            public float Unknown1;       // ButtonClick 0.5, IncomingCall_* 0 (meaning not known: unused)
            public float Unknown2;       // all 0
            public float Unknown3;       // all 0
            public float Unknown4;       // all 0
            public float Unknown5;       // all 0
            public AudioMixerGroup MixerGroup; // live's "UI" group (not shipped); SPT's "UI" group is used instead
        }

        /// <summary>Set by the plugin: SPT's "UI" mixer group.</summary>
        public static Func<AudioMixerGroup> UiMixerGroup;

        [SerializeField] public Sound[] _sounds;
        [SerializeField] public UnityEngine.Object _unsetReference;
        [SerializeField] public AudioSource _sourceA;
        [SerializeField] public AudioSource _sourceB;

        int _played;

        public void PlaySoundBank(string bankType)
        {
            if (_sounds == null) return;
            Sound s = null;
            foreach (var x in _sounds) if (x != null && x.Name == bankType) { s = x; break; }
            if (s == null || s.Clip == null) { Debug.Log("[ServerRoomBackground] radio: no sound '" + bankType + "'"); return; }
            var src = s.SourceIndex == 1 ? _sourceB : _sourceA;
            if (src == null) src = _sourceA != null ? _sourceA : _sourceB;
            if (src == null) return;
            var g = UiMixerGroup?.Invoke();
            if (g != null) src.outputAudioMixerGroup = g;
            src.pitch = s.Pitch;
            src.PlayOneShot(s.Clip, s.Volume);
            if (_played++ < 3 || bankType.StartsWith("IncomingCall"))
                Debug.Log($"[ServerRoomBackground] radio: {bankType} ({s.Clip.name}, vol {s.Volume}, source {(src == _sourceA ? "A" : "B")}, mixer {(g != null ? g.name : "none")})");
        }

        public void StopAll()
        {
            if (_sourceA != null) _sourceA.Stop();
            if (_sourceB != null) _sourceB.Stop();
        }
    }
}
