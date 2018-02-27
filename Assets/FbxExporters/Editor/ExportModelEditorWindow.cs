﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using FbxExporters.EditorTools;

namespace FbxExporters
{
    namespace Editor
    {
        public class ExportModelEditorWindow : EditorWindow
        {

            private const string WindowTitle = "Export Options";
            private const float SelectableLabelMinWidth = 90;
            private const float BrowseButtonWidth = 25;
            private const float LabelWidth = 144;
            private const float FieldOffset = 18;
            private string m_exportFileName = "";
            private ModelExporter.AnimationExportType m_animExportType = ModelExporter.AnimationExportType.all;

            public static void Init (string filename = "", ModelExporter.AnimationExportType exportType = ModelExporter.AnimationExportType.all)
            {
                ExportModelEditorWindow window = (ExportModelEditorWindow)EditorWindow.GetWindow <ExportModelEditorWindow>(WindowTitle, focus:true);
                window.SetFilename (filename);
                window.SetAnimationExportType (exportType);
                window.minSize = new Vector2 (SelectableLabelMinWidth + LabelWidth + BrowseButtonWidth, 100);
                window.Show ();
            }

            public void SetFilename(string filename){
                m_exportFileName = filename;
            }

            public void SetAnimationExportType(ModelExporter.AnimationExportType exportType){
                m_animExportType = exportType;
            }

            void OnGUI ()
            {
                // Increasing the label width so that none of the text gets cut off
                EditorGUIUtility.labelWidth = LabelWidth;

                EditorGUILayout.LabelField("Naming", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent(
                    "Export Path:",
                    "Relative path for saving Model Prefabs."),GUILayout.Width(LabelWidth - FieldOffset));

                var pathLabel = ExportSettings.GetRelativeSavePath();
                if (pathLabel == ".") { pathLabel = "(Assets root)"; }
                EditorGUILayout.SelectableLabel(pathLabel,
                    EditorStyles.textField,
                    GUILayout.MinWidth(SelectableLabelMinWidth),
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));            

                if (GUILayout.Button(new GUIContent("...", "Browse to a new location for saving model prefabs"), EditorStyles.miniButton, GUILayout.Width(BrowseButtonWidth)))
                {
                    string initialPath = ExportSettings.GetAbsoluteSavePath();

                    // if the directory doesn't exist, set it to the default save path
                    // so we don't open somewhere unexpected
                    if (!System.IO.Directory.Exists(initialPath))
                    {
                        initialPath = Application.dataPath;
                    }

                    string fullPath = EditorUtility.OpenFolderPanel(
                        "Select Model Prefabs Path", initialPath, null
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
                            ExportSettings.SetRelativeSavePath(relativePath);

                            // Make sure focus is removed from the selectable label
                            // otherwise it won't update
                            GUIUtility.hotControl = 0;
                            GUIUtility.keyboardControl = 0;
                        }
                    }
                }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal ();
                EditorGUILayout.LabelField(new GUIContent(
                    "Export Name:",
                    "Filename to save model to."),GUILayout.Width(LabelWidth - FieldOffset));

                m_exportFileName = EditorGUILayout.TextField (m_exportFileName);
                if (!m_exportFileName.EndsWith (".fbx")) {
                    m_exportFileName += ".fbx";
                }
                m_exportFileName = ModelExporter.ConvertToValidFilename(m_exportFileName);
                GUILayout.EndHorizontal ();

                GUILayout.FlexibleSpace ();

                GUILayout.BeginHorizontal ();
                GUILayout.FlexibleSpace ();
                if (GUILayout.Button ("Cancel")) {
                    this.Close ();
                }

                if (GUILayout.Button ("Export")) {
                    var filePath = ExportSettings.GetAbsoluteSavePath();
                    filePath = System.IO.Path.Combine (filePath, m_exportFileName);

                    //TODO: check if file already exists, give a warning if it does
                    if (ModelExporter.ExportObjects (filePath, exportType: m_animExportType, lodExportType: ExportSettings.instance.lodExportType) != null) {
                        // refresh the asset database so that the file appears in the
                        // asset folder view.
                        AssetDatabase.Refresh ();
                    }
                    this.Close ();
                }
                GUILayout.EndHorizontal ();
            }
        }
    }
}