using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Etazh.Prototype.Editor
{
    public static class PrototypeSceneSetup
    {
        private const string Folder = "Assets/_Project/Prototype/Scenes";
        public const string ScenePath = Folder + "/ElevatorPrototype.unity";
        private const string GameScenePath = Folder + "/ElevatorGame.unity";

        public static void CreateScene()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("The existing prototype scene is owned by the project.");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(11, 22, 32, 255);
            camera.orthographic = true;
            camera.transform.position = new Vector3(0, 0, -10);

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1280, 720);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 1;
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Folder + "/PrototypeTheme.tss");
            if (panel.themeStyleSheet == null)
                throw new InvalidOperationException("The prototype UI theme was not imported.");

            AssetDatabase.CreateAsset(panel, Folder + "/PrototypePanel.asset");

            var root = new GameObject("Elevator Prototype");
            var document = root.AddComponent<UIDocument>();
            document.panelSettings = panel;
            var controller = root.AddComponent<PrototypeController>();
            using (var serialized = new SerializedObject(controller))
            {
                serialized.FindProperty("_document").objectReferenceValue = document;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("The prototype scene could not be saved.");

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.productName = "Этаж, пожалуйста";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            AssetDatabase.SaveAssets();
            Debug.Log("Etazh prototype scene created at " + ScenePath);
        }

        public static void BuildWindows()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(arguments, "-buildOutput");
            if (index < 0 || index + 1 >= arguments.Length)
                throw new InvalidOperationException("Provide the Windows executable path through -buildOutput.");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { GameScenePath },
                target = BuildTarget.StandaloneWindows64,
                locationPathName = arguments[index + 1],
                options = BuildOptions.Development
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException($"Prototype build failed: {report.summary.result}");
        }
    }
}
