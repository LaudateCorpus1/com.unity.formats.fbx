﻿// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using FbxSdk;

namespace FbxExporters
{
    namespace Editor
    {
        public class ConvertToModel : System.IDisposable
        {
            const string MenuItemName1 = "Assets/Convert To Model";
            const string MenuItemName2 = "GameObject/Convert To Model";
            
            /// <summary>
            /// Clean up this class on garbage collection
            /// </summary>
            public void Dispose () { }

            /// <summary>
            /// create menu item in the File menu
            /// </summary>
            [MenuItem (MenuItemName1, false)]
            public static void OnMenuItem ()
            {
            	OnConvertInPlace ();
            }

            /// <summary>
            // Validate the menu item defined by the function above.
            /// </summary>
            [MenuItem (MenuItemName1, true)]
            public static bool OnValidateMenuItem ()
            {
            	return true;
            }

            // Add a menu item called "Export Model..." to a GameObject's context menu.
            [MenuItem (MenuItemName2, false, 30)]
            static void OnContextItem (MenuCommand command)
            {
            	OnConvertInPlace ();
            }

            private static List<GameObject> OnConvertInPlace ()
            {
                List<GameObject> result = new List<GameObject> ();

                GameObject[] unityActiveGOs = Selection.GetFiltered<GameObject>(SelectionMode.Editable | SelectionMode.TopLevel);

                // find common ancestor root & filePath;
                string filePath = "";
                string dirPath = Path.Combine (Application.dataPath, "Objects");

                GameObject unityCommonAncestor = null;
                int siblingIndex = -1;

                foreach (GameObject goObj in unityActiveGOs)
                {
                    siblingIndex = goObj.transform.GetSiblingIndex ();
                    unityCommonAncestor = goObj.transform.parent.gameObject;
                    filePath = Path.Combine (dirPath, goObj.name + ".fbx");

                    break;
                }

                string fbxFileName = FbxExporters.Editor.ModelExporter.ExportObjects (filePath, unityActiveGOs) as string;

                if (fbxFileName!=null)
                {
                    // make filepath relative to project folder
                    if (fbxFileName.StartsWith(Application.dataPath, System.StringComparison.CurrentCulture)) {
                        fbxFileName = "Assets" + fbxFileName.Substring(Application.dataPath.Length);
                    }

                    // refresh the assetdata base so that we can query for the model
                    AssetDatabase.Refresh ();

                    // replace w Model asset
                    Object unityMainAsset = AssetDatabase.LoadMainAssetAtPath (fbxFileName);

                    if (unityMainAsset != null) {
                        Transform unityParentTransform = 
                            (unityCommonAncestor==null) ? null : unityCommonAncestor.transform;

                        Object unityObj = PrefabUtility.InstantiateAttachedAsset(unityMainAsset);

                        if (unityObj!=null)
                        {
                            GameObject unityGO = unityObj as GameObject;

                            // configure name
                            const string cloneSuffix = "(Clone)";

                            if (unityGO.name.EndsWith(cloneSuffix,System.StringComparison.CurrentCulture))
                            {
                                unityGO.name = unityGO.name.Remove(cloneSuffix.Length-1);  
                            }

                            // configure transform
                            unityGO.transform.parent = unityParentTransform;
                            unityGO.transform.SetSiblingIndex (siblingIndex);

                            result.Add (unityObj as GameObject);

                            // remove (now redundant) gameobjects
                            for (int i = 0; i < unityActiveGOs.Length; i++) {
                                unityActiveGOs [i].name = "_safe_to_delete_" + unityActiveGOs [i].name;
                                // Object.DestroyImmediate(unityActiveGOs[i]);
                            }
                        }
                    }

                }
                return result;
            }
        }
    }
}