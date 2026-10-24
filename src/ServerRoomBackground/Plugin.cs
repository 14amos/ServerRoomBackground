using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Audio;
using BepInEx;
using BepInEx.Logging;
using EFT;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

namespace ServerRoomBackground
{
    /// <summary>
    /// Live EFT's "server room" main menu background (live scene EnvironmentUIScene_Season_LAB, video Season_BD) as a
    /// selectable background, registered like WTT-ContentBackport's: enum value from the prepatcher, an EnvironmentData
    /// entry on EnvironmentUI, bundles loaded when it is picked. The scene and its assets are the live files repacked
    /// (tools/Repack); 5 live-only scripts are rebuilt in ServerRoomBackground.Scripts.
    /// </summary>
    [BepInPlugin("com.amos.serverroombackground", "Server Room Background", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string TypeName = "ServerRoomEnvironmentUiType";
        public const string SceneName = "EnvironmentUIScene_Season_LAB";
        public const string ScenePath = "Assets/Scenes/UI/Seasonal/" + SceneName + ".unity";
        const string AssetsBundle = "serverroom_assets.bundle", SceneBundle = "serverroom_scene.bundle";

        internal static ManualLogSource Log;
        static string dir;
        static EEnvironmentUIType type;
        static bool typeOk;
        static AssetBundle assets, scene;
        static AudioMixerGroup uiGroup;

        public void Awake()
        {
            Log = Logger;
            dir = Path.GetDirectoryName(Info.Location);
            typeOk = Enum.GetNames(typeof(EEnvironmentUIType)).Contains(TypeName);
            if (typeOk) type = (EEnvironmentUIType)Enum.Parse(typeof(EEnvironmentUIType), TypeName);
            Log.LogInfo($"[ServerRoomBackground] enum {TypeName} {(typeOk ? "= " + (int)type : "MISSING (prepatcher not run): background disabled")}; folder {dir}");
            if (!typeOk) return;

            StreamingVideoPlayer.ResolvePath = rel =>
            {
                var p = Path.Combine(dir, rel);
                return File.Exists(p) ? p : null;
            };
            AnimationEventLocalSoundPlayer.UiMixerGroup = UiGroup;
            SceneManager.sceneLoaded += OnSceneLoaded;

            var h = new Harmony("com.amos.serverroombackground");
            h.Patch(AccessTools.Method(typeof(EnvironmentUI), "Awake"), prefix: new HarmonyMethod(typeof(Plugin), nameof(AwakePrefix)));
            h.Patch(AccessTools.Method(typeof(EnvironmentUI), nameof(EnvironmentUI.SetEnvironmentAsync)), prefix: new HarmonyMethod(typeof(Plugin), nameof(SetEnvironmentPrefix)));
            h.Patch(AccessTools.Method(typeof(EnvironmentUIRoot), nameof(EnvironmentUIRoot.RandomRotate)), prefix: new HarmonyMethod(typeof(Plugin), nameof(RandomRotatePrefix)));
            h.Patch(AccessTools.Method(typeof(EnvironmentUIRoot), nameof(EnvironmentUIRoot.RotateBack)), prefix: new HarmonyMethod(typeof(Plugin), nameof(RotateBackPrefix)));
            h.Patch(AccessTools.Method(typeof(EnvironmentUIRoot), nameof(EnvironmentUIRoot.ResetRotation)), prefix: new HarmonyMethod(typeof(Plugin), nameof(ResetRotationPrefix)));
            Log.LogInfo("[ServerRoomBackground] patches on");
        }

        static void AwakePrefix(EnvironmentUI __instance)
        {
            var list = __instance._environments?.ToList() ?? new List<EnvironmentUI.EnvironmentData>();
            if (list.Any(x => x.Type == type)) return;
            list.Add(new EnvironmentUI.EnvironmentData { Type = type, SceneName = SceneName, EligibleVersions = Array.Empty<string>() });
            __instance._environments = list.ToArray();
            Log.LogInfo($"[ServerRoomBackground] EnvironmentUI: added {TypeName} -> scene {SceneName} ({list.Count} backgrounds: {string.Join(", ", list.Select(x => x.Type))})");
        }

        static void SetEnvironmentPrefix(EEnvironmentUIType environmentUiType)
        {
            if (environmentUiType != type) return;
            if (!LoadBundles()) Log.LogError("[ServerRoomBackground] bundles not loaded: the background will not show");
        }

        static bool LoadBundles()
        {
            if (assets != null && scene != null) return true;
            try
            {
                var t = DateTime.Now;
                if (assets == null) assets = AssetBundle.LoadFromFile(Path.Combine(dir, AssetsBundle));
                if (assets == null) { Log.LogError("[ServerRoomBackground] failed to load " + AssetsBundle); return false; }
                if (scene == null) scene = AssetBundle.LoadFromFile(Path.Combine(dir, SceneBundle));
                if (scene == null) { Log.LogError("[ServerRoomBackground] failed to load " + SceneBundle); return false; }
                var paths = scene.GetAllScenePaths();
                Log.LogInfo($"[ServerRoomBackground] bundles loaded in {(DateTime.Now - t).TotalMilliseconds:0} ms; scene bundle has [{string.Join(", ", paths)}]");
                if (!paths.Any(p => string.Equals(p, ScenePath, StringComparison.OrdinalIgnoreCase))) { Log.LogError("[ServerRoomBackground] scene bundle lacks " + ScenePath); return false; }
                var video = Path.Combine(dir, "Video", "EnvironmentUIBg", "Season_BD", "main_menu-server's_room(animate_exp).webm");
                Log.LogInfo("[ServerRoomBackground] video file " + (File.Exists(video) ? "present" : "MISSING: " + video));
                return true;
            }
            catch (Exception e) { Log.LogError("[ServerRoomBackground] bundle load failed: " + e); return false; }
        }

        /// <summary>Before EnvironmentUI activates the scene's camera: SPT's own post-processing resources (live's are not
        /// shipped) and a log of what loaded.</summary>
        static void OnSceneLoaded(Scene s, LoadSceneMode mode)
        {
            if (s.name != SceneName) return;
            try
            {
                var roots = s.GetRootGameObjects();
                var res = Resources.FindObjectsOfTypeAll<PostProcessResources>().FirstOrDefault();
                int layers = 0;
                foreach (var r in roots)
                    foreach (var l in r.GetComponentsInChildren<PostProcessLayer>(true))
                    {
                        // Without resources the layer would throw every frame: leave it off instead.
                        if (res != null) l.Init(res); else l.enabled = false;
                        layers++;
                    }
                Log.LogInfo($"[ServerRoomBackground] scene loaded: roots [{string.Join(", ", roots.Select(r => r.name))}], {layers} PostProcessLayer(s) given {(res != null ? res.name : "NO PostProcessResources (none loaded)")}");
                var missing = roots.SelectMany(r => r.GetComponentsInChildren<Component>(true)).Count(c => c == null);
                if (missing > 0) Log.LogError($"[ServerRoomBackground] {missing} missing script(s) in the scene");
            }
            catch (Exception e) { Log.LogError("[ServerRoomBackground] sceneLoaded: " + e); }
        }

        static AudioMixerGroup UiGroup()
        {
            if (uiGroup != null) return uiGroup;
            try
            {
                // The mixer GUISounds itself loads (GUISounds.Init: Resources "Audio/MasterMixer", group "UI").
                var mixer = Resources.Load<AudioMixer>("Audio/MasterMixer");
                uiGroup = mixer != null ? mixer.FindMatchingGroups("UI").FirstOrDefault() : null;
                Log.LogInfo("[ServerRoomBackground] UI mixer group " + (uiGroup != null ? uiGroup.name : "NOT found"));
            }
            catch (Exception e) { Log.LogError("[ServerRoomBackground] mixer: " + e); }
            return uiGroup;
        }

        static HorizontalEnvironmentCameraMotion Motion(EnvironmentUIRoot root) =>
            root != null && root.CameraContainer != null ? root.CameraContainer.GetComponent<HorizontalEnvironmentCameraMotion>() : null;

        // Live's camera container slides between its Main / Left / Right points instead of SPT's random rotation.
        static bool RandomRotatePrefix(EnvironmentUIRoot __instance) { var m = Motion(__instance); if (m == null) return true; m.MoveToRandomSide(); return false; }
        static bool RotateBackPrefix(EnvironmentUIRoot __instance) { var m = Motion(__instance); if (m == null) return true; m.MoveToMain(); return false; }
        static bool ResetRotationPrefix(EnvironmentUIRoot __instance) { var m = Motion(__instance); if (m == null) return true; m.ResetPosition(); return false; }
    }
}
