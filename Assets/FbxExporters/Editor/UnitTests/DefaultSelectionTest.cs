﻿using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using NUnit.Framework;
using System.IO;
using System.Collections.Generic;

namespace FbxExporters.UnitTests
{
    /// <summary>
    /// Tests the default selection export behavior.
    /// Tests that the right GameObjects are exported and
    /// that they have the expected transforms.
    /// </summary>
    public class DefaultSelectionTest
    {
        private string _filePath;
        protected string filePath       { get { return string.IsNullOrEmpty(_filePath) ? Application.dataPath : _filePath; } set { _filePath = value; } }

        private string _fileNamePrefix;
        protected string fileNamePrefix { get { return string.IsNullOrEmpty(_fileNamePrefix) ? "_safe_to_delete__" : _fileNamePrefix; }
            set { _fileNamePrefix = value; } }

        private string _fileNameExt;
        protected string fileNameExt    { get { return string.IsNullOrEmpty(_fileNameExt) ? ".fbx" : _fileNameExt; } set { _fileNameExt = value; } }

        private string MakeFileName(string baseName = null, string prefixName = null, string extName = null)
        {
            if (baseName==null)
                baseName = Path.GetRandomFileName();

            if (prefixName==null)
                prefixName = this.fileNamePrefix;

            if (extName==null)
                extName = this.fileNameExt;

            return prefixName + baseName + extName;
        }

        protected string GetRandomFileNamePath(string pathName = null, string prefixName = null, string extName = null)
        {
            string temp;

            if (pathName==null)
                pathName = this.filePath;

            if (prefixName==null)
                prefixName = this.fileNamePrefix;

            if (extName==null)
                extName = this.fileNameExt;

            // repeat until you find a file that does not already exist
            do {
                temp = Path.Combine (pathName, MakeFileName(prefixName: prefixName, extName: extName));

            } while(File.Exists (temp));

            return temp;
        }

        [TearDown]
        public void Term ()
        {
            foreach (string file in Directory.GetFiles (this.filePath, MakeFileName("*"))) {
                File.Delete (file);
            }
        }

        [Test]
        public void TestDefaultSelection ()
        {
            var root = CreateHierarchy ();
            Assert.IsNotNull (root);

            // test Export Root
            // Expected result: everything gets exported
            var exportedRoot = ExportSelection (new Object[]{root});
            CompareHierarchies(root, exportedRoot, true);

            // test Export Parent1, Child1
            // Expected result: Parent1, Child1, Child2
            var parent1 = root.transform.Find("Parent1");
            var child1 = parent1.Find ("Child1");
            exportedRoot = ExportSelection (new Object[]{parent1.gameObject, child1.gameObject});
            CompareHierarchies(parent1.gameObject, exportedRoot, true);

            // test Export Child2
            // Expected result: Child2
            var child2 = parent1.Find("Child2").gameObject;
            exportedRoot = ExportSelection (new Object[]{child2});
            CompareHierarchies(child2, exportedRoot, true);

            // test Export Child2, Parent2
            // Expected result: Parent2, Child3, Child2
            var parent2 = root.transform.Find("Parent2");
            exportedRoot = ExportSelection (new Object[]{child2, parent2});

            List<GameObject> children = new List<GameObject> ();
            foreach (Transform child in exportedRoot.transform) {
                children.Add (child.gameObject);
            }
            CompareHierarchies(new GameObject[]{child2, parent2.gameObject}, children.ToArray());

            UnityEngine.Object.DestroyImmediate (root);
        }

        private GameObject CreateHierarchy ()
        {
            // Create the following hierarchy:
            //      Root
            //      -> Parent1
            //      ----> Child1
            //      ----> Child2
            //      -> Parent2
            //      ----> Child3

            var root = CreateGameObject ("Root");

            var parent1 = CreateGameObject ("Parent1", root.transform);
            var parent2 = CreateGameObject ("Parent2", root.transform);
            parent1.transform.SetAsFirstSibling ();

            CreateGameObject ("Child1", parent1.transform);
            CreateGameObject ("Child2", parent1.transform);
            CreateGameObject ("Child3", parent2.transform);

            return root;
        }

        private GameObject CreateGameObject(string name, Transform parent = null)
        {
            var go = new GameObject (name);
            go.transform.SetParent (parent);
            return go;
        }

        private void CompareHierarchies(GameObject expectedHierarchy, GameObject actualHierarchy, bool ignoreName = false)
        {
            if (!ignoreName) {
                Assert.AreEqual (expectedHierarchy.name, actualHierarchy.name);
            }

            var expectedTransform = expectedHierarchy.transform;
            var actualTransform = actualHierarchy.transform;
            Assert.AreEqual (expectedTransform.childCount, actualTransform.childCount);

            foreach (Transform expectedChild in expectedTransform) {
                var actualChild = actualTransform.Find (expectedChild.name);
                Assert.IsNotNull (actualChild);
                CompareHierarchies (expectedChild.gameObject, actualChild.gameObject);
            }
        }

        private void CompareHierarchies(GameObject[] expectedHierarchy, GameObject[] actualHierarchy)
        {
            Assert.AreEqual (expectedHierarchy.Length, actualHierarchy.Length);

            System.Array.Sort (expectedHierarchy, delegate (GameObject x, GameObject y) {
                return x.name.CompareTo(y.name);
            });
            System.Array.Sort (actualHierarchy, delegate (GameObject x, GameObject y) {
                return x.name.CompareTo(y.name);
            });

            for (int i = 0; i < expectedHierarchy.Length; i++) {
                CompareHierarchies (expectedHierarchy [i], actualHierarchy [i]);
            }
        }

        private GameObject ExportSelection(Object[] selected)
        {
            // export selected to a file, then return the root
            var filename = GetRandomFileNamePath();

            Debug.unityLogger.logEnabled = false;
            var fbxFileName = FbxExporters.Editor.ModelExporter.ExportObjects (filename, selected) as string;
            Debug.unityLogger.logEnabled = true;

            Assert.IsNotNull (fbxFileName);

            // make filepath relative to project folder
            if (fbxFileName.StartsWith (Application.dataPath, System.StringComparison.CurrentCulture)) 
            {
                fbxFileName = "Assets" + fbxFileName.Substring (Application.dataPath.Length);
            }
            // refresh the assetdata base so that we can query for the model
            AssetDatabase.Refresh ();

            Object unityMainAsset = AssetDatabase.LoadMainAssetAtPath (fbxFileName);
            var fbxRoot = unityMainAsset as GameObject;

            Assert.IsNotNull (fbxRoot);
            return fbxRoot;
        }
    }
}