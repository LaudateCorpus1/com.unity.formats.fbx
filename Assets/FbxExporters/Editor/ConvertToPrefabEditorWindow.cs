﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using FbxExporters.EditorTools;
using UnityEditor.Presets;

namespace FbxExporters
{
    namespace Editor
    {
        public class ConvertToPrefabEditorWindow : ExportOptionsEditorWindow
        {
            protected override GUIContent m_windowTitle { get { return new GUIContent ("Convert Options"); }}
            private GameObject[] m_toConvert;
            private string m_prefabFileName = "";

            public static void Init (IEnumerable<GameObject> toConvert)
            {
                ConvertToPrefabEditorWindow window = CreateWindow<ConvertToPrefabEditorWindow> ();
                window.InitializeWindow (filename: "", singleHierarchyExport: true, exportType: ModelExporter.AnimationExportType.all);
                window.SetGameObjectsToConvert (toConvert);
                window.Show ();
            }

            protected void SetGameObjectsToConvert(IEnumerable<GameObject> toConvert){
                var tempList = new List<GameObject> ();
                foreach (var go in toConvert) {
                    tempList.Add (go);
                }
                m_toConvert = tempList.ToArray ();

                if (m_toConvert.Length >= 1) {
                    m_prefabFileName = m_toConvert [0].name;
                    this.SetFilename (m_prefabFileName);
                }
            }

            protected override void OnEnable ()
            {
                base.OnEnable ();

                if (!m_innerEditor) {
                    var ms = ExportSettings.instance.convertToPrefabSettings;
                    if (!ms) {
                        ms = ExportSettings.instance.convertToPrefabSettings;
                    }
                    m_innerEditor = UnityEditor.Editor.CreateEditor (ms);
                }
            }

            protected override void Export ()
            {
                var fbxDirPath = ExportSettings.GetFbxAbsoluteSavePath ();
                var fbxPath = System.IO.Path.Combine (fbxDirPath, m_exportFileName + ".fbx");

                var prefabDirPath = ExportSettings.GetPrefabAbsoluteSavePath ();
                var prefabPath = System.IO.Path.Combine (prefabDirPath, m_prefabFileName + ".prefab");

                // check if file already exists, give a warning if it does
                if (!OverwriteExistingFile (fbxPath) || !OverwriteExistingFile (prefabPath)) {
                    return;
                }

                if (m_toConvert == null) {
                    Debug.LogError ("FbxExporter: missing object for conversion");
                    return;
                }

                if (m_toConvert.Length == 1) {
                    ConvertToModel.Convert (m_toConvert[0], fbxFullPath: fbxPath, prefabFullPath: prefabPath);
                    return;
                }

                foreach (var go in m_toConvert) {
                    ConvertToModel.Convert (go, fbxDirectoryFullPath: fbxDirPath, prefabDirectoryFullPath: prefabDirPath);
                }
            }

            protected override bool DisableNameSelection ()
            {
                return m_toConvert.Length > 1;
            }

            protected override void CreateCustomUI ()
            {
                GUILayout.BeginHorizontal ();
                EditorGUILayout.LabelField(new GUIContent(
                    "Prefab Name:",
                    "Filename to save prefab to."),GUILayout.Width(LabelWidth-FieldOffset));

                EditorGUI.BeginDisabledGroup (DisableNameSelection());
                m_prefabFileName = EditorGUILayout.TextField (m_prefabFileName);
                m_prefabFileName = ModelExporter.ConvertToValidFilename (m_prefabFileName);
                EditorGUI.EndDisabledGroup ();
                GUILayout.EndHorizontal ();

                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent(
                    "Prefab Path:",
                    "Relative path for saving Linked Prefabs."),GUILayout.Width(LabelWidth - FieldOffset));

                var pathLabels = ExportSettings.GetRelativePrefabSavePaths();

                ExportSettings.instance.selectedPrefabPath = EditorGUILayout.Popup (ExportSettings.instance.selectedPrefabPath, pathLabels, GUILayout.MinWidth(SelectableLabelMinWidth));

                if (GUILayout.Button(new GUIContent("...", "Browse to a new location to save prefab to"), EditorStyles.miniButton, GUILayout.Width(BrowseButtonWidth)))
                {
                    string initialPath = Application.dataPath;

                    string fullPath = EditorUtility.OpenFolderPanel(
                        "Select Linked Prefab Save Path", initialPath, null
                    );

                    // Unless the user canceled, make sure they chose something in the Assets folder.
                    if (!string.IsNullOrEmpty(fullPath))
                    {
                        var relativePath = ExportSettings.ConvertToAssetRelativePath(fullPath);
                        if (string.IsNullOrEmpty(relativePath))
                        {
                            Debug.LogWarning("Please select a location in the Assets folder");
                        }
                        else
                        {
                            ExportSettings.AddPrefabSavePath(relativePath);

                            // Make sure focus is removed from the selectable label
                            // otherwise it won't update
                            GUIUtility.hotControl = 0;
                            GUIUtility.keyboardControl = 0;
                        }
                    }
                }
                GUILayout.EndHorizontal();
            }
        }	
	}
}
