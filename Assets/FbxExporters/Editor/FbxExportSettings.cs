﻿using System;
using System.IO;
using UnityEditorInternal;
using UnityEngine;
using UnityEditor;

namespace FbxSdk.EditorTools {

    [CustomEditor(typeof(FbxExportSettings))]
    public class FbxExportSettingsEditor : Editor {
        public override void OnInspectorGUI() {
            FbxExportSettings temp = (FbxExportSettings)target;

            temp.weldVertices = EditorGUILayout.Toggle ("Weld Vertices:", temp.weldVertices);

            if (GUI.changed) {
                EditorUtility.SetDirty (temp);
                temp.Dirty ();
            }
        }
    }

    [FilePath("ProjectSettings/FbxExportSettings.asset",FilePathAttribute.Location.ProjectFolder)]
    public class FbxExportSettings : FbxSdk.EditorTools.ScriptableSingleton<FbxExportSettings>
    {
        public bool weldVertices = true;

        [MenuItem("Edit/Project Settings/Fbx Export", priority = 300)]
        static void ShowManager()
        {
            instance.name = "Fbx Export Settings";
            Selection.activeObject = instance;
        }

        public void Dirty()
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
                    ScriptableSingleton<T>.CreateAndLoad();
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
        private static void CreateAndLoad()
        {
            string filePath = ScriptableSingleton<T>.GetFilePath();
            if (!string.IsNullOrEmpty(filePath))
            {
                var loaded = InternalEditorUtility.LoadSerializedFileAndForget(filePath);
                if (loaded.Length > 0) {
                    ScriptableSingleton<T>.s_Instance = loaded [0] as T;
                    return;
                }
            }
            if (ScriptableSingleton<T>.s_Instance == null)
            {
                T t = ScriptableObject.CreateInstance<T>();
                //t.hideFlags = HideFlags.HideAndDontSave;
                ScriptableSingleton<T>.s_Instance = t;
            }
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
