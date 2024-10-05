﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeardPhantom.Bootstrap.Editor
{
    public class BootstrapWizard : ScriptableWizard
    {
        [field: Delayed]
        [field: SerializeField]
        private string OutputDirectory { get; set; } = "Assets/Bootstrap";

        [field: Delayed]
        [field: SerializeField]
        private string SceneName { get; set; } = "Bootstrap";

        [field: Delayed]
        [field: SerializeField]
        private string ServicesPrefabName { get; set; } = "Services";

        [field: SerializeField]
        private bool ServicesPrefabInRoot { get; set; } = true;

        private string ScenePath => $"{OutputDirectory}{SceneName}.unity";

        private string BootstrapperPrefabPath => $"{OutputDirectory}Bootstrapper.prefab";

        private string ServicesPrefabPath =>
            ServicesPrefabInRoot
                ? $"Assets/{ServicesPrefabName}.prefab"
                : $"{OutputDirectory}{ServicesPrefabName}.prefab";

        [MenuItem("Edit/Bootstrap Wizard")]
        private static void Open()
        {
            DisplayWizard<BootstrapWizard>("Bootstrap Wizard", "Run Setup");
        }

        private static bool DoesAssetPathExist(string path)
        {
            return !string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(path));
        }

        /// <inheritdoc />
        protected override bool DrawWizardGUI()
        {
            using var serializedObject = new SerializedObject(this);
            SerializedProperty serializedProperty = serializedObject.GetIterator();

            // Enter first property and skip m_Script and m_SerializedDataModeController
            serializedProperty.NextVisible(true);
            serializedProperty.NextVisible(false);
            serializedProperty.NextVisible(false);
            do
            {
                // if(serializedProperty.propertyPath is not "m_Script" or "")
                EditorGUILayout.PropertyField(serializedProperty, true);
            }
            while (serializedProperty.NextVisible(false));

            DrawOutputPaths();

            return serializedObject.ApplyModifiedProperties();
        }

        private void DrawOutputPaths()
        {
            EditorGUILayout.Separator();
            EditorGUILayout.LabelField("Output Paths", EditorStyles.boldLabel);

            const string AssetExistsWarn = "Asset exists at this path and will be overwritten.";

            bool assetPathExists = DoesAssetPathExist(ScenePath);
            GUI.contentColor = assetPathExists ? Color.yellow : Color.white;
            string tooltip = assetPathExists ? AssetExistsWarn : null;
            EditorGUILayout.LabelField(new GUIContent("Scene Path", tooltip), new GUIContent(ScenePath, tooltip));

            assetPathExists = DoesAssetPathExist(BootstrapperPrefabPath);
            GUI.contentColor = assetPathExists ? Color.yellow : Color.white;
            tooltip = assetPathExists ? AssetExistsWarn : null;
            EditorGUILayout.LabelField(
                new GUIContent("Bootstrap Prefab Path", tooltip),
                new GUIContent(BootstrapperPrefabPath, tooltip));

            assetPathExists = DoesAssetPathExist(ServicesPrefabPath);
            GUI.contentColor = assetPathExists ? Color.yellow : Color.white;
            tooltip = assetPathExists ? AssetExistsWarn : null;
            EditorGUILayout.LabelField(
                new GUIContent("Services Prefab Path", tooltip),
                new GUIContent(ServicesPrefabPath, tooltip));

            GUI.contentColor = Color.white;
        }

        private void OnWizardUpdate()
        {
            FixPathsAndNames();
        }

        private void FixPathsAndNames()
        {
            if (!OutputDirectory.StartsWith("Assets/"))
            {
                OutputDirectory = $"Assets/{OutputDirectory}";
            }

            if (!OutputDirectory.EndsWith("/"))
            {
                OutputDirectory = $"{OutputDirectory}/";
            }

            if (string.IsNullOrEmpty(SceneName))
            {
                SceneName = "Bootstrap";
            }

            if (string.IsNullOrEmpty(ServicesPrefabName))
            {
                ServicesPrefabName = "Services";
            }
        }

        private void OnEnable()
        {
            FixPathsAndNames();
        }

        private void OnWizardCreate()
        {
            Scene bootstrapScene = default;
            try
            {
                Directory.CreateDirectory(OutputDirectory);

                bootstrapScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                bool didSave = EditorSceneManager.SaveScene(bootstrapScene, ScenePath);
                if (!didSave)
                {
                    Debug.LogError("Bootstrap scene not saved, cancelling asset creation.");
                    return;
                }

                // Create services
                var servicesObj = new GameObject(ServicesPrefabName);
                GameObject servicesPrefab = PrefabUtility.SaveAsPrefabAsset(servicesObj, ServicesPrefabPath, out bool success);
                DestroyImmediate(servicesObj);
                if (!success)
                {
                    Debug.LogError("Services prefab not saved, cancelling asset creation.");
                    return;
                }

                var boostrapperGObj = new GameObject("Bootstrapper");
                SceneManager.MoveGameObjectToScene(boostrapperGObj, bootstrapScene);
                var bootstrapper = boostrapperGObj.AddComponent<Bootstrapper>();
                var prefabLoader = PrefabProvider.Create<DirectPrefabProvider>(boostrapperGObj, servicesPrefab);
                bootstrapper.PrefabProvider = prefabLoader;
                PrefabUtility.SaveAsPrefabAssetAndConnect(
                    boostrapperGObj,
                    BootstrapperPrefabPath,
                    InteractionMode.AutomatedAction,
                    out success);
                if (!success)
                {
                    Debug.LogError("Bootstrap prefab not saved.");
                }

                EditorSceneManager.SaveScene(bootstrapScene);

                List<EditorBuildSettingsScene> scenesList = EditorBuildSettings.scenes.ToList();
                scenesList.RemoveAll(a => a.path == ScenePath);
                scenesList.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenesList.ToArray();
            }
            finally
            {
                if (bootstrapScene.IsValid() && bootstrapScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(bootstrapScene, true);
                }
            }
        }
    }
}