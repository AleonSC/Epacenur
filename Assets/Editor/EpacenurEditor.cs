using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Epacenur.EditorTools
{
    /// Creates the (empty) scene and build settings on first open, and adds an "Epacenur" menu.
    /// The game builds its own world when Play is pressed (Game.cs), so the scene stays empty.
    [InitializeOnLoad]
    static class EpacenurEditor
    {
        const string ScenePath = "Assets/Scenes/DevArea.unity";
        const string BuildPath = "Build/Epacenur/Epacenur.exe";

        static EpacenurEditor()
        {
            EditorApplication.delayCall += EnsureSetup;
        }

        static void EnsureSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!File.Exists(ScenePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log("Epacenur: created " + ScenePath + ". Press Play to run the game.");
            }
            if (EditorBuildSettings.scenes.Length == 0)
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Epacenur";
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
        }

        [MenuItem("Epacenur/Open Dev Area Scene")]
        static void OpenScene()
        {
            EnsureSetup();
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Epacenur/Delete Saved Progress")]
        static void DeleteSave()
        {
            PlayerPrefs.DeleteKey(Runtime.SaveGame.Key);
            Debug.Log("Epacenur: saved skills and inventory deleted.");
        }

        [MenuItem("Epacenur/Build Windows Game")]
        static void BuildWindows()
        {
            EnsureSetup();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = BuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Epacenur: built " + BuildPath + ".");
                EditorUtility.RevealInFinder(BuildPath);
            }
            else
            {
                Debug.LogError("Epacenur: build " + report.summary.result + ". Is Windows Build Support installed (Unity Hub > Installs > Add modules)?");
            }
        }
    }
}
