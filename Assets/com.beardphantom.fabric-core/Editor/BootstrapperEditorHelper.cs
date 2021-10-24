﻿using Fabric.Core.Runtime;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fabric.Core.Editor
{
    [InitializeOnLoad]
    public static class BootstrapperEditorHelper
    {
        #region Fields

        public static bool EnableSceneManagement = true;

        #endregion

        #region Constructors

        static BootstrapperEditorHelper()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        #endregion

        #region Methods

        private static void OnPlayModeStateChanged(PlayModeStateChange mode)
        {
            switch (mode)
            {
                case PlayModeStateChange.ExitingEditMode:
                {
                    if (EnableSceneManagement)
                    {
                        PrepareForEnteringPlayMode();
                    }

                    break;
                }
                case PlayModeStateChange.EnteredEditMode:
                {
                    App.CleanupEditorOnly();
                    break;
                }
            }
        }

        private static void PrepareForEnteringPlayMode()
        {
            SessionState.EraseString(EditorBootstrapHandler.LOADED_SCENES_KEY);
            SessionState.EraseString(EditorBootstrapHandler.SERIALIZED_BOOTSTRAPPER_JSON_KEY);
            EditorSceneManager.playModeStartScene = null;

            var bootstrapper = Object.FindObjectOfType<Bootstrapper>();
            if (bootstrapper == null || !bootstrapper.isActiveAndEnabled)
            {
                return;
            }

            FabricLog.Logger.Log(LogTags.FABRIC_CORE, "Running bootstrap helper...");

            var bootstrapScene = EditorBuildSettings.scenes.FirstOrDefault(
                s => AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path) != null);
            if (bootstrapScene == null)
            {
                FabricLog.Logger.LogWarning(LogTags.FABRIC_CORE, "No valid first scene in EditorBuildSettings");
            }
            else
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(bootstrapScene.path);
                EditorSceneManager.playModeStartScene = sceneAsset;

                var scenePaths = new List<string>();
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.buildIndex != 0)
                    {
                        scenePaths.Add(scene.path);
                    }
                }

                var scenesString = string.Join(
                    EditorBootstrapHandler.LOADED_SCENES_SEPARATOR.ToString(),
                    scenePaths);
                SessionState.SetString(EditorBootstrapHandler.LOADED_SCENES_KEY, scenesString);

                var serializedBootstrapperJson = EditorJsonUtility.ToJson(bootstrapper);
                if (!string.IsNullOrWhiteSpace(serializedBootstrapperJson))
                {
                    SessionState.SetString(
                        EditorBootstrapHandler.SERIALIZED_BOOTSTRAPPER_JSON_KEY,
                        serializedBootstrapperJson);
                }
            }
        }

        #endregion
    }
}