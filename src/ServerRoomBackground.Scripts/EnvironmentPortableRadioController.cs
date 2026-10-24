using Audio;
using UnityEngine;

namespace UI.Utilities
{
    // Rebuilt from live EFT 1.2.5 scene data (class missing in SPT 0.16.9). Serialized layout = live's 28 bytes:
    // the radio's AnimationEventLocalSoundPlayer, bool (1), the radio's LCD Animator. In the scene the LCD Animator is
    // DISABLED (m_Enabled 0) and nothing else references it, so this component is what starts it: the LCD animation
    // (Portable_radio_LCD_Screens_Animation, 1350+ s) drives the screen and fires the ButtonClick / IncomingCall_01..05
    // PlaySoundBank events. What live does beyond that is not known.
    public class EnvironmentPortableRadioController : MonoBehaviour
    {
        [SerializeField] public AnimationEventLocalSoundPlayer _soundPlayer;
        [SerializeField] public bool _enableAnimator = true;
        [SerializeField] public Animator _lcdAnimator;

        public void OnEnable()
        {
            if (_enableAnimator && _lcdAnimator != null) _lcdAnimator.enabled = true;
            var ctrl = _lcdAnimator != null && _lcdAnimator.runtimeAnimatorController != null ? _lcdAnimator.runtimeAnimatorController.name : "none";
            Debug.Log($"[ServerRoomBackground] radio on (sound player {_soundPlayer != null}, LCD animator {ctrl}, enabled {(_lcdAnimator != null && _lcdAnimator.enabled)})");
        }
    }
}
