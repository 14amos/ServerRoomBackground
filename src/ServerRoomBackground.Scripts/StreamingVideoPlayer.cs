using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace EFT.UI
{
    // Rebuilt from live EFT 1.2.5 scene data (class missing in SPT 0.16.9). Serialized layout = live's 96 bytes:
    // VideoPlayer, path under StreamingAssets ("Video\EnvironmentUIBg\Season_BD\...webm"), bool (1 in the scene).
    // The VideoPlayer itself carries the render setup (material override _EmissionMap, looping, no audio).
    public class StreamingVideoPlayer : MonoBehaviour
    {
        [SerializeField] public VideoPlayer _videoPlayer;
        [SerializeField] public string _videoPath;
        [SerializeField] public bool _playOnEnable;

        /// <summary>Set by the plugin: path under StreamingAssets -> file in the mod's folder (null = use StreamingAssets).</summary>
        public static System.Func<string, string> ResolvePath;

        public void OnEnable()
        {
            if (_playOnEnable) Play();
        }

        public void OnDisable()
        {
            if (_videoPlayer != null) _videoPlayer.Stop();
        }

        public void Play()
        {
            if (_videoPlayer == null || string.IsNullOrEmpty(_videoPath)) { Debug.Log("[ServerRoomBackground] video: no player or path"); return; }
            var rel = _videoPath.Replace('\\', '/');
            var full = ResolvePath?.Invoke(rel) ?? Path.Combine(Application.streamingAssetsPath, rel);
            if (!File.Exists(full)) { Debug.LogError("[ServerRoomBackground] video file missing: " + full); return; }
            _videoPlayer.source = VideoSource.Url;
            _videoPlayer.url = full;
            _videoPlayer.errorReceived -= OnError;
            _videoPlayer.errorReceived += OnError;
            _videoPlayer.prepareCompleted -= OnPrepared;
            _videoPlayer.prepareCompleted += OnPrepared;
            _videoPlayer.Prepare();
            Debug.Log("[ServerRoomBackground] video preparing " + full);
        }

        void OnPrepared(VideoPlayer vp)
        {
            vp.Play();
            Debug.Log($"[ServerRoomBackground] video playing {vp.width}x{vp.height} {vp.frameRate:0.##} fps, {vp.length:0.#} s, loop {vp.isLooping}");
        }

        void OnError(VideoPlayer vp, string message) => Debug.LogError("[ServerRoomBackground] video error: " + message);
    }
}
