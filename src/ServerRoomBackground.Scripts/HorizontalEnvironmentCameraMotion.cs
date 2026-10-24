using DG.Tweening;
using UnityEngine;

namespace EFT.UI
{
    // Rebuilt from live EFT 1.2.5 scene data (class missing in SPT 0.16.9). Serialized layout = live's 48 bytes:
    // Transform "Main" (x 0), Transform[] { "Left" (x -140), "Right" (x +140) }, float 1 (= SPT's EnvironmentUIRoot
    // ROTATE_DURATION), Ease 16 (InOutQuint). It sits on the camera container, so it replaces SPT's random camera
    // rotation (EnvironmentUIRoot.RandomRotate / RotateBack / ResetRotation, patched by the plugin) with a slide.
    public class HorizontalEnvironmentCameraMotion : MonoBehaviour
    {
        [SerializeField] public Transform _mainPoint;
        [SerializeField] public Transform[] _sidePoints;
        [SerializeField] public float _duration = 1f;
        [SerializeField] public Ease _ease = Ease.InOutQuint;

        // The points are children of the Environment UI Root, like the camera container: their local positions are cached.
        Vector3 _main;
        Vector3[] _sides;
        bool _cached;

        public void Awake() => Cache();

        public void Cache()
        {
            if (_cached) return;
            _main = _mainPoint != null ? _mainPoint.localPosition : Vector3.zero;
            int n = _sidePoints?.Length ?? 0;
            _sides = new Vector3[n];
            for (int i = 0; i < n; i++) _sides[i] = _sidePoints[i] != null ? _sidePoints[i].localPosition : _main;
            _cached = true;
            Debug.Log($"[ServerRoomBackground] camera motion: main {_main}, {n} side points, {_duration} s, {_ease}");
        }

        public void MoveToRandomSide()
        {
            Cache();
            if (_sides.Length == 0) return;
            MoveTo(_sides[Random.Range(0, _sides.Length)]);
        }

        public void MoveToMain()
        {
            Cache();
            MoveTo(_main);
        }

        public void ResetPosition()
        {
            Cache();
            transform.DOKill();
            transform.localPosition = _main;
        }

        void MoveTo(Vector3 target)
        {
            transform.DOKill();
            if (gameObject.activeInHierarchy) transform.DOLocalMove(target, _duration).SetEase(_ease);
            else transform.localPosition = target;
        }
    }
}
