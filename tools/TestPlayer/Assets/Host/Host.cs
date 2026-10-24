using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

// Test host for the repacked server-room bundles (Unity 2022.3.43 player like SPT's client). EFT classes here are
// field-only stubs, so post effects and EFT logic do not run: this checks loading, scripts binding, data, video, render.
public class Host : MonoBehaviour
{
    const string SceneName = "EnvironmentUIScene_Season_LAB";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() { new GameObject("host").AddComponent<Host>(); }

    static void L(string s) { Debug.Log("[TestHost] " + s); }

    IEnumerator Start()
    {
        var dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "bundles");
        var a = AssetBundle.LoadFromFile(Path.Combine(dir, "serverroom_assets.bundle"));
        L("assets bundle " + (a != null) + (a != null ? ", " + a.GetAllAssetNames().Length + " assets" : ""));
        var s = AssetBundle.LoadFromFile(Path.Combine(dir, "serverroom_scene.bundle"));
        L("scene bundle " + (s != null) + (s != null ? ", scenes [" + string.Join(", ", s.GetAllScenePaths()) + "]" : ""));
        if (a == null || s == null) { Application.Quit(1); yield break; }
        var op = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
        yield return op;
        var sc = SceneManager.GetSceneByName(SceneName);
        var roots = sc.GetRootGameObjects();
        L($"scene loaded {sc.isLoaded}, roots [{string.Join(", ", roots.Select(r => r.name))}]");
        int comps = 0, missing = 0;
        foreach (var r in roots)
            foreach (var co in r.GetComponentsInChildren<Component>(true)) { comps++; if (co == null) missing++; }
        L($"components {comps}, missing scripts {missing}");
        foreach (var r in roots)
            foreach (var mb in r.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null) L($"  script {mb.GetType().Assembly.GetName().Name}:{mb.GetType().FullName} on {mb.gameObject.name}");
        // Check the rebuilt scripts' data.
        var root = roots.First().GetComponent<EFT.UI.VideoEnvironmentUIRoot>();
        L($"root: CameraContainer {(root.CameraContainer ? root.CameraContainer.name : "null")}, Shading {(root.Shading ? root.Shading.name : "null")}, videoHolder {(root._videoHolderGameObject ? root._videoHolderGameObject.name : "null")}, logos {(root._regularLogo ? root._regularLogo.name : "null")}/{(root._pveLogo ? root._pveLogo.name : "null")}");
        var svp = roots.SelectMany(r => r.GetComponentsInChildren<EFT.UI.StreamingVideoPlayer>(true)).FirstOrDefault();
        L($"StreamingVideoPlayer: player {(svp?._videoPlayer != null)}, path '{svp?._videoPath}', playOnEnable {svp?._playOnEnable}");
        var cam = roots.SelectMany(r => r.GetComponentsInChildren<EFT.UI.HorizontalEnvironmentCameraMotion>(true)).FirstOrDefault();
        L($"CameraMotion: main {(cam?._mainPoint ? cam._mainPoint.name : "null")}, sides [{string.Join(", ", cam?._sidePoints?.Select(t => t ? t.name : "null") ?? new string[0])}], {cam?._duration} s, {cam?._ease}");
        var snd = roots.SelectMany(r => r.GetComponentsInChildren<Audio.AnimationEventLocalSoundPlayer>(true)).FirstOrDefault();
        L($"SoundPlayer: {snd?._sounds?.Length} sounds [{string.Join(", ", snd?._sounds?.Select(x => $"{x.Name}:{(x.Clip ? x.Clip.name : "null")}:{x.SourceIndex}:{x.Volume}:{x.Pitch}") ?? new string[0])}], A {(snd?._sourceA ? snd._sourceA.name : "null")}, B {(snd?._sourceB ? snd._sourceB.name : "null")}");
        var rc = roots.SelectMany(r => r.GetComponentsInChildren<UI.Utilities.EnvironmentPortableRadioController>(true)).FirstOrDefault();
        L($"RadioController: player {(rc?._soundPlayer != null)}, enableAnimator {rc?._enableAnimator}, animator {(rc?._lcdAnimator ? rc._lcdAnimator.runtimeAnimatorController?.name : "null")}");
        var keep = roots.SelectMany(r => r.GetComponentsInChildren<AnimatorKeepStateOnDisable>(true)).FirstOrDefault();
        L($"KeepState: {keep?._keepState}");
        // Shaders: anything that failed to load renders pink; list unsupported ones.
        foreach (var rend in roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)))
            foreach (var m in rend.sharedMaterials)
                if (m != null && (m.shader == null || !m.shader.isSupported)) L($"  UNSUPPORTED shader {m.shader?.name} on {rend.name}");
        // EnvironmentUI equivalent: move the scene's root under us, enable the camera (stub EnvironmentUIRoot has no Init).
        foreach (var l in roots.SelectMany(r => r.GetComponentsInChildren<PostProcessLayer>(true))) l.enabled = false; // no PostProcessResources here
        root.CameraContainer.gameObject.SetActive(true);
        var c = root.CameraContainer.GetComponentInChildren<Camera>();
        L($"camera {c.name} fov {c.fieldOfView} near {c.nearClipPlane} far {c.farClipPlane} pos {c.transform.position}");
        var vp = svp?._videoPlayer;
        for (int i = 0; i < 12; i++)
        {
            yield return new WaitForSeconds(1f);
            if (vp != null && i % 3 == 0) L($"video t={vp.time:0.0} playing {vp.isPlaying} prepared {vp.isPrepared} url {vp.url}");
        }
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Application.dataPath), "shot_main.png"));
        yield return new WaitForSeconds(1f);
        cam.MoveToRandomSide();
        yield return new WaitForSeconds(2f);
        ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Application.dataPath), "shot_side.png"));
        yield return new WaitForSeconds(1f);
        L($"side camera pos {cam.transform.localPosition}");
        cam.MoveToMain();
        yield return new WaitForSeconds(2f);
        L($"main camera pos {cam.transform.localPosition}; radio LCD enabled {rc?._lcdAnimator?.enabled}, state time {(rc?._lcdAnimator ? rc._lcdAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime : -1):0.00000}");
        snd?.PlaySoundBank("ButtonClick");
        yield return new WaitForSeconds(1f);
        L("done");
        Application.Quit(0);
    }
}
