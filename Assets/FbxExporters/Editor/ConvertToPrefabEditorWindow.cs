﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using FbxExporters.EditorTools;
using UnityEditor.Presets;
using System.Linq;

namespace FbxExporters
{
    namespace Editor
    {
        public class ConvertToPrefabEditorWindow : ExportOptionsEditorWindow
        {
            protected override GUIContent WindowTitle { get { return new GUIContent ("Convert Options"); }}
            protected override float MinWindowHeight { get { return 280; } } // determined by trial and error
            protected override string ExportButtonName { get { return "Convert"; } }
            private GameObject[] m_toConvert;
            private string m_prefabFileName = "";

            private float m_prefabExtLabelWidth;

            protected override Transform TransferAnimationSource {
                get {
                    return ExportSettings.instance.convertToPrefabSettings.info.AnimationSource;
                }
                set {
                    var selectedGO = ModelExporter.GetGameObject(m_toConvert[0]);
                    if (!TransferAnimationSourceIsValid (value, selectedGO)) {
                        return;
                    }
                    ExportSettings.instance.convertToPrefabSettings.info.SetAnimationSource (value);
                }
            }

            protected override Transform TransferAnimationDest {
                get {
                    return ExportSettings.instance.convertToPrefabSettings.info.AnimationDest;
                }
                set {
                    var selectedGO = ModelExporter.GetGameObject(m_toConvert[0]);
                    if (!TransferAnimationDestIsValid (value, selectedGO)) {
                        return;
                    }
                    ExportSettings.instance.convertToPrefabSettings.info.SetAnimationDest (value);
                }
            }

            public static void Init (IEnumerable<GameObject> toConvert)
            {
                ConvertToPrefabEditorWindow window = CreateWindow<ConvertToPrefabEditorWindow> ();
                window.InitializeWindow ();
                window.SetGameObjectsToConvert (toConvert);
                window.Show ();
            }

            protected void SetGameObjectsToConvert(IEnumerable<GameObject> toConvert){
                m_toConvert = toConvert.OrderBy (go => go.name).ToArray ();

                if (m_toConvert.Length == 1) {
                    m_prefabFileName = m_toConvert [0].name;

                    // if only one object selected, set transfer source/dest to this object
                    var go = ModelExporter.GetGameObject (m_toConvert [0]);
                    if (go) {
                        TransferAnimationSource = go.transform;
                        TransferAnimationDest = go.transform;
                    }
                } else if (m_toConvert.Length > 1) {
                    m_prefabFileName = "(automatic)";
                }

                DisableTransferAnim = DisableNameSelection = m_toConvert.Length > 1;

                this.SetFilename (m_prefabFileName);
            }

            protected override void OnEnable ()
            {
                base.OnEnable ();
                if (!m_innerEditor) {
                    m_innerEditor = UnityEditor.Editor.CreateEditor (ExportSettings.instance.convertToPrefabSettings);
                }
                m_prefabExtLabelWidth = m_fbxExtLabelStyle.CalcSize (new GUIContent (".prefab")).x;
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
                    ConvertToModel.Convert (
                        m_toConvert[0], fbxFullPath: fbxPath, prefabFullPath: prefabPath, exportOptions: ExportSettings.instance.convertToPrefabSettings.info
                    );
                    return;
                }

                foreach (var go in m_toConvert) {
                    ConvertToModel.Convert (
                        go, fbxDirectoryFullPath: fbxDirPath, prefabDirectoryFullPath: prefabDirPath, exportOptions: ExportSettings.instance.convertToPrefabSettings.info
                    );
                }
            }

            protected override void ShowPresetReceiver ()
            {
                ShowPresetReceiver (ExportSettings.instance.convertToPrefabSettings);
            }

            protected override void CreateCustomUI ()
            {
                GUILayout.BeginHorizontal ();
                EditorGUILayout.LabelField(new GUIContent(
                    "Prefab Name:",
                    "Filename to save prefab to."),GUILayout.Width(LabelWidth-TextFieldAlignOffset));

                EditorGUI.BeginDisabledGroup (DisableNameSelection);
                // Show the export name with an uneditable ".prefab" at the end
                //-------------------------------------
                EditorGUILayout.BeginVertical ();
                EditorGUILayout.BeginHorizontal(EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUI.indentLevel--;
                // continually resize to contents
                var textFieldSize = m_nameTextFieldStyle.CalcSize (new GUIContent(m_prefabFileName));
                m_prefabFileName = EditorGUILayout.TextField (m_prefabFileName, m_nameTextFieldStyle, GUILayout.Width(textFieldSize.x + 5), GUILayout.MinWidth(5));
                m_prefabFileName = ModelExporter.ConvertToValidFilename (m_prefabFileName);

                EditorGUILayout.LabelField ("<color=#808080ff>.prefab</color>", m_fbxExtLabelStyle, GUILayout.Width(m_prefabExtLabelWidth));
                EditorGUI.indentLevel++;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical ();
                //-----------------------------------
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
