using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Video;

// Builds two bundles WITH type trees for the server-room repack (Unity 2022.3.43f1, the SPT client's version):
// Out/oracle_assets.bundle: one prefab per script class in classes.txt ("assembly|full.type.name") + one asset of every
// engine asset class the scene's closure uses; Out/oracle_scene.bundle: a scene holding every engine component class.
public static class MenuOracle
{
    public static void Run()
    {
        Directory.CreateDirectory("Assets/Gen");
        var names = new List<string>();
        int ok = 0, fail = 0;
        var asms = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var line in File.ReadAllLines("classes.txt"))
        {
            if (line.Trim().Length == 0) continue;
            var p = line.Split('|');
            var asm = asms.FirstOrDefault(a => a.GetName().Name == p[0]);
            var t = asm?.GetType(p[1]);
            if (t == null) { Debug.Log("[MenuOracle] NOT FOUND " + line); fail++; continue; }
            if (typeof(MonoBehaviour).IsAssignableFrom(t))
            {
                var go = new GameObject(t.FullName);
                if (t.IsSubclassOf(typeof(UnityEngine.EventSystems.UIBehaviour))) go.AddComponent<RectTransform>();
                Component c = null;
                try { c = go.AddComponent(t); } catch (Exception e) { Debug.Log("[MenuOracle] AddComponent threw " + line + ": " + e.Message); }
                if (c == null || c.GetType() != t) { Debug.Log("[MenuOracle] FAILED " + line); fail++; UnityEngine.Object.DestroyImmediate(go); continue; }
                var path = "Assets/Gen/mb_" + ok + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go); names.Add(path); ok++;
            }
            else if (typeof(ScriptableObject).IsAssignableFrom(t))
            {
                var path = "Assets/Gen/so_" + ok + ".asset";
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance(t), path); names.Add(path); ok++;
            }
        }
        // Engine asset classes.
        var tex = new Texture2D(4, 4); File.WriteAllBytes("Assets/Gen/t.png", tex.EncodeToPNG());
        File.WriteAllText("Assets/Gen/s.shader", "Shader \"Oracle/S\" { SubShader { Pass { } } }");
        WriteWav("Assets/Gen/a.wav");
        AssetDatabase.Refresh();
        names.Add("Assets/Gen/t.png"); names.Add("Assets/Gen/s.shader"); names.Add("Assets/Gen/a.wav");
        var mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat, "Assets/Gen/m.mat"); names.Add("Assets/Gen/m.mat");
        var mesh = new Mesh { vertices = new[] { Vector3.zero, Vector3.up, Vector3.right }, triangles = new[] { 0, 1, 2 } };
        AssetDatabase.CreateAsset(mesh, "Assets/Gen/me.asset"); names.Add("Assets/Gen/me.asset");
        var t3 = new Texture3D(2, 2, 2, TextureFormat.RGBA32, false); AssetDatabase.CreateAsset(t3, "Assets/Gen/t3.asset"); names.Add("Assets/Gen/t3.asset");
        var cube = new Cubemap(4, TextureFormat.RGBA32, false); AssetDatabase.CreateAsset(cube, "Assets/Gen/c.cubemap"); names.Add("Assets/Gen/c.cubemap");
        var clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, "Assets/Gen/clip.anim"); names.Add("Assets/Gen/clip.anim");
        var legacy = new AnimationClip { legacy = true }; AssetDatabase.CreateAsset(legacy, "Assets/Gen/legacy.anim"); names.Add("Assets/Gen/legacy.anim");
        var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath("Assets/Gen/ctrl.controller");
        ctrl.AddMotion(clip); names.Add("Assets/Gen/ctrl.controller");

        // Engine components, in a scene (scene settings objects come with it) and in a prefab.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("engine");
        foreach (var t in new[] { typeof(Camera), typeof(Light), typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(Rigidbody),
                                  typeof(AudioSource), typeof(Animator), typeof(Animation), typeof(VideoPlayer), typeof(StreamingController) })
            root.AddComponent(t);
        var skin = new GameObject("skin", typeof(SkinnedMeshRenderer)); skin.transform.SetParent(root.transform);
        var ui = new GameObject("ui", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup), typeof(CanvasRenderer)); ui.transform.SetParent(root.transform);
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Gen/engine.prefab"); names.Add("Assets/Gen/engine.prefab");
        EditorSceneManager.SaveScene(scene, "Assets/Gen/oracle.unity");
        AssetDatabase.Refresh();

        Directory.CreateDirectory("Out");
        BuildPipeline.BuildAssetBundles("Out", new[]
        {
            new AssetBundleBuild { assetBundleName = "oracle_assets.bundle", assetNames = names.ToArray() },
            new AssetBundleBuild { assetBundleName = "oracle_scene.bundle", assetNames = new[] { "Assets/Gen/oracle.unity" } },
        }, BuildAssetBundleOptions.UncompressedAssetBundle, BuildTarget.StandaloneWindows64);
        Debug.Log($"[MenuOracle] done: {ok} script classes, {fail} failed");
    }

    static void WriteWav(string path)
    {
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int n = 4410; w.Write("RIFF".ToCharArray()); w.Write(36 + n * 2); w.Write("WAVEfmt ".ToCharArray()); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(44100); w.Write(88200); w.Write((short)2); w.Write((short)16); w.Write("data".ToCharArray()); w.Write(n * 2);
            for (int i = 0; i < n; i++) w.Write((short)0);
        }
    }
}
