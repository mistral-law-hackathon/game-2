using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildScript
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string OutputPath = "webgl";

    [MenuItem("Game/Regenerate Main Scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cam = new GameObject("Main Camera") { tag = "MainCamera" };
        var c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = Color.black;
        cam.AddComponent<AudioListener>();
        cam.transform.position = new Vector3(0, 0, -10);
        new GameObject("CardGame").AddComponent<CardGame>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    [MenuItem("Game/Build WebGL")]
    public static void BuildWebGL()
    {
        CreateScene();
        PlayerSettings.companyName = "MistralLawHackathon";
        PlayerSettings.productName = "OBJECTION! The AI Card Duel";
        PlayerSettings.runInBackground = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = false;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        });
        Debug.Log("BUILD RESULT: " + report.summary.result + " size=" + report.summary.totalSize);
        if (Application.isBatchMode)
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
