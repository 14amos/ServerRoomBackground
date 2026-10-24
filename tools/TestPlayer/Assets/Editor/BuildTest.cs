using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildTest
{
    public static void Run()
    {
        Directory.CreateDirectory("Assets/Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Boot.unity");
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 });
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Scenes/Boot.unity" }, "Build/TestPlayer.exe", BuildTarget.StandaloneWindows64, BuildOptions.Development);
        Debug.Log("[BuildTest] " + r.summary.result + " errors " + r.summary.totalErrors);
    }
}
