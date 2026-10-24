using UnityEngine;

// Rebuilt from live EFT 1.2.5 scene data (class missing in SPT 0.16.9). Serialized layout = live's 4 bytes: bool (1).
// On the radio, next to its LCD Animator: keeps the 1350 s LCD animation's place when the menu scene is hidden.
public class AnimatorKeepStateOnDisable : MonoBehaviour
{
    [SerializeField] public bool _keepState = true;

    public void Awake()
    {
        var a = GetComponent<Animator>();
        if (a != null) a.keepAnimatorStateOnDisable = _keepState;
    }
}
