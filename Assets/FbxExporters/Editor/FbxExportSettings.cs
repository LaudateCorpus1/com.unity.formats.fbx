﻿using System;
using System.IO;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor;

namespace FbxExporters.EditorTools {

    [CustomEditor(typeof(ExportSettings))]
    public class ExportSettingsEditor : UnityEditor.Editor {
        public override void OnInspectorGUI() {
            ExportSettings exportSettings = (ExportSettings)target;

            // Increasing the label width so that none of the text gets cut off
            EditorGUIUtility.labelWidth = 300;

            exportSettings.weldVertices = EditorGUILayout.Toggle ("Weld Vertices:", exportSettings.weldVertices);
            exportSettings.embedTextures = EditorGUILayout.Toggle ("Embed Textures:", exportSettings.embedTextures);
            exportSettings.mayaCompatibleNames = EditorGUILayout.Toggle (
                new GUIContent("Convert to Maya Compatible Naming:",
                    "In Maya some symbols such as spaces and accents get replaced when importing an FBX " +
                    "(e.g. \"foo bar\" becomes \"fooFBXASC032bar\"). " +
                    "On export, convert the names of GameObjects so they are Maya compatible." +
                    (exportSettings.mayaCompatibleNames? "" : 
                        "\n\nWARNING: Disabling this feature may result in lost material connections," +
                        " and unexpected character replacements in Maya.")
                ),
                exportSettings.mayaCompatibleNames);

            if (GUI.changed) {
                EditorUtility.SetDirty (exportSettings);
                exportSettings.Save ();
            }
        }
    }

    [FilePath("ProjectSettings/FbxExportSettings.asset",FilePathAttribute.Location.ProjectFolder)]
    public class ExportSettings : FbxExporters.EditorTools.ScriptableSingleton<ExportSettings>
    {
        public bool weldVertices = true;
        public bool embedTextures = false;
        public bool mayaCompatibleNames = true;

        [MenuItem("Edit/Project Settings/Fbx Export", priority = 300)]
        static void ShowManager()
        {
            instance.name = "Fbx Export Settings";
            Selection.activeObject = instance;
        }

        public void Save()
        {
            instance.Save (true);
        }
    }

    public class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        private static T s_Instance;
        public static T instance
        {
            get
            {
                if (ScriptableSingleton<T>.s_Instance == null)
                {
                    return ScriptableSingleton<T>.CreateAndLoad();
                }
                return ScriptableSingleton<T>.s_Instance;
            }
        }

        protected ScriptableSingleton()
        {
            if (ScriptableSingleton<T>.s_Instance != null)
            {
                Debug.LogError("ScriptableSingleton already exists. Did you query the singleton in a constructor?");
            }
        }
        private static T CreateAndLoad()
        {
            string filePath = ScriptableSingleton<T>.GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                var loaded = InternalEditorUtility.LoadSerializedFileAndForget(filePath);

                if (loaded.Length > 0) {
                    ScriptableSingleton<T>.s_Instance = loaded [0] as T;
                }
            }
            if (ScriptableSingleton<T>.s_Instance == null)
            {
                T t = ScriptableObject.CreateInstance<T>();
                ScriptableSingleton<T>.s_Instance = t;
            }
            return ScriptableSingleton<T>.s_Instance;
        }
        protected virtual void Save(bool saveAsText)
        {
            if (ScriptableSingleton<T>.s_Instance == null)
            {
                Debug.Log("Cannot save ScriptableSingleton: no instance!");
                return;
            }
            string filePath = ScriptableSingleton<T>.GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                string directoryName = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                InternalEditorUtility.SaveToSerializedFileAndForget(new T[]
                    {
                        ScriptableSingleton<T>.s_Instance
                    }, filePath, saveAsText);
            }
        }
        private static string GetFilePath()
        {
            Type typeFromHandle = typeof(T);
            object[] customAttributes = typeFromHandle.GetCustomAttributes(true);
            object[] array = customAttributes;
            for (int i = 0; i < array.Length; i++)
            {
                object obj = array[i];
                if (obj is FilePathAttribute)
                {
                    FilePathAttribute filePathAttribute = obj as FilePathAttribute;
                    return filePathAttribute.filepath;
                }
            }
            return null;
        }
    }


    [AttributeUsage(AttributeTargets.Class)]
    public class FilePathAttribute : Attribute
    {
        public enum Location
        {
            PreferencesFolder,
            ProjectFolder
        }
        public string filepath
        {
            get;
            set;
        }
        public FilePathAttribute(string relativePath, FilePathAttribute.Location location)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                Debug.LogError("Invalid relative path! (its null or empty)");
                return;
            }
            if (relativePath[0] == '/')
            {
                relativePath = relativePath.Substring(1);
            }
            if (location == FilePathAttribute.Location.PreferencesFolder)
            {
                this.filepath = InternalEditorUtility.unityPreferencesFolder + "/" + relativePath;
            }
            else
            {
                this.filepath = relativePath;
            }
        }
    }

}
