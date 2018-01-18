using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using System.IO;
using System.Collections.Generic;
using FbxExporters.Editor;

namespace FbxExporters.UnitTests
{
    /// <summary>
    /// Test that the post-import prefab updater works properly,
    /// by triggering it to run.
    /// </summary>
    public class FbxPrefabAutoUpdaterTest : ExporterTestBase
    {
        GameObject m_fbx;
        string m_fbxPath;
        GameObject m_prefab;
        string m_prefabPath;

        [SetUp]
        public void Init ()
        {
            var capsule = GameObject.CreatePrimitive (PrimitiveType.Capsule);
            m_fbx = ExportSelection (capsule);
            m_fbxPath = AssetDatabase.GetAssetPath (m_fbx);

            // Instantiate the fbx and create a prefab from it.
            // Delete the object right away (don't even wait for term).
            var fbxInstance = PrefabUtility.InstantiatePrefab (m_fbx) as GameObject;
            new FbxPrefabAutoUpdater.FbxPrefabUtility (fbxInstance.AddComponent<FbxPrefab> ()).SetSourceModel (m_fbx);
            m_prefabPath = GetRandomPrefabAssetPath ();
            m_prefab = PrefabUtility.CreatePrefab (m_prefabPath, fbxInstance);
            AssetDatabase.Refresh ();
            Assert.AreEqual (m_prefabPath, AssetDatabase.GetAssetPath (m_prefab));
            GameObject.DestroyImmediate (fbxInstance);
        }

        [Test]
        public void BasicTest ()
        {
            var fbxPrefabPath = FbxPrefabAutoUpdater.FindFbxPrefabAssetPath ();
            Assert.IsFalse (string.IsNullOrEmpty (fbxPrefabPath));
            Assert.IsTrue (fbxPrefabPath.EndsWith (FbxPrefabAutoUpdater.FBX_PREFAB_FILE));

            Assert.IsTrue (FbxPrefabAutoUpdater.IsFbxAsset ("Assets/path/to/foo.fbx"));
            Assert.IsFalse (FbxPrefabAutoUpdater.IsFbxAsset ("Assets/path/to/foo.png"));

            Assert.IsTrue (FbxPrefabAutoUpdater.IsPrefabAsset ("Assets/path/to/foo.prefab"));
            Assert.IsFalse (FbxPrefabAutoUpdater.IsPrefabAsset ("Assets/path/to/foo.fbx"));
            Assert.IsFalse (FbxPrefabAutoUpdater.IsPrefabAsset ("Assets/path/to/foo.png"));

            var imported = new HashSet<string> (new string [] { "Assets/path/to/foo.fbx", m_fbxPath });
            Assert.IsTrue (FbxPrefabAutoUpdater.MayHaveFbxPrefabToFbxAsset (m_prefabPath, fbxPrefabPath,
                        imported));
        }

        [Test]
        public void RectTransformTest ()
        {
            Vector3 scaleForward = new Vector3 (1, 2, 3);
            Vector3 positionForward = new Vector3 (100, 200, 300);
            Vector3 rotationForward = new Vector3 (1, 2, 3);

            Vector3 scaleBackward = new Vector3 (3, 2, 1);
            Vector3 positionBackward = new Vector3 (300, 200, 100);
            Vector3 rotationBackward = new Vector3 (3, 2, 1);

            //Create a hierarchy with a RectTransform
            var cube = GameObject.CreatePrimitive (PrimitiveType.Cube);
            var capsule = GameObject.CreatePrimitive (PrimitiveType.Capsule);
            capsule.transform.SetParent (cube.transform);

            var rectTransform = capsule.AddComponent<RectTransform> ();

            rectTransform.localScale = scaleForward;
            rectTransform.localPosition = positionForward;
            rectTransform.localRotation = Quaternion.Euler (rotationForward);
#if UNITY_2017_3_OR_NEWER
            rectTransform.ForceUpdateRectTransforms ();
#endif

            string filePath1 = GetRandomFbxFilePath ();

            //instantiate our hierarchy as a prefab
            var oldInstance = ConvertToModel.Convert (cube, fbxFullPath: filePath1);
            Assert.IsTrue (oldInstance);

            rectTransform = oldInstance.transform.GetChild (0).GetComponent<RectTransform> ();

            Assert.IsTrue (rectTransform.localScale == scaleForward);
            Assert.IsTrue (rectTransform.localPosition == positionForward);
            Assert.IsTrue (rectTransform.localRotation == Quaternion.Euler (rotationForward));

            //Create an "updated" hierarchy
            var cube2 = GameObject.CreatePrimitive (PrimitiveType.Cube);
            var capsule2 = GameObject.CreatePrimitive (PrimitiveType.Capsule);
            capsule2.transform.SetParent (cube2.transform);

            rectTransform = capsule2.AddComponent<RectTransform> ();

            rectTransform.localScale = scaleBackward;
            rectTransform.localPosition = positionBackward;
            rectTransform.localRotation = Quaternion.Euler (rotationBackward);
#if UNITY_2017_3_OR_NEWER
            rectTransform.ForceUpdateRectTransforms ();
#endif
            //export our updated hierarchy to the same file path as the original
            SleepForFileTimestamp();
            FbxExporters.Editor.ModelExporter.ExportObject(filePath1, cube2);
            AssetDatabase.Refresh();

            rectTransform = oldInstance.transform.GetChild (0).GetComponent<RectTransform> ();
            Assert.IsTrue(rectTransform.localScale == scaleBackward);
            Assert.IsTrue(rectTransform.localPosition == positionBackward);
            Assert.IsTrue(rectTransform.localRotation == Quaternion.Euler(rotationBackward));

            GameObject.DestroyImmediate(cube);
            GameObject.DestroyImmediate(cube2);
        }

        [Test]
        public void ReplaceTest ()
        {
            // Instantiate the prefab.
            var oldInstance = PrefabUtility.InstantiatePrefab(m_prefab) as GameObject;
            Assert.IsTrue(oldInstance);

            // Create a new hierarchy. It's marked for delete already.
            var newHierarchy = CreateHierarchy();

            // Export it to the same fbx path. But first, wait one second so
            // that its timestamp differs enough for Unity to notice it
            // changed.
            SleepForFileTimestamp();
            FbxExporters.Editor.ModelExporter.ExportObject(m_fbxPath, newHierarchy);
            AssetDatabase.Refresh();

            // Verify that a new instance of the prefab got updated.
            var newInstance = PrefabUtility.InstantiatePrefab(m_prefab) as GameObject;
            Assert.IsTrue(newInstance);
            AssertSameHierarchy(newHierarchy, newInstance, ignoreRootName: true, ignoreRootTransform: true);

            // Verify that the old instance also got updated.
            AssertSameHierarchy(newHierarchy, oldInstance, ignoreRootName: true, ignoreRootTransform: true);
        }

    }

    public class FbxPrefabAutoUpdaterRemappingTest : ExporterTestBase
    {
        [Test]
        public void RemappingTest()
        {
            //Create a hierarchy of objects
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(cube.transform);
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.SetParent(sphere.transform);

            // Convert to linked prefab instance (auto-updating prefab)
            GameObject cubePrefabInstance = ConvertToModel.Convert(cube);
            Object cubePrefabParent = PrefabUtility.GetPrefabParent(cubePrefabInstance);

            // In FbxPrefab Component of Cube, add SphereFBX/Sphere name mapping
            FbxPrefab fbxPrefabScript = cubePrefabInstance.transform.GetComponent<FbxPrefab>();
            FbxPrefab.StringPair stringpair = new FbxPrefab.StringPair();
            stringpair.FBXObjectName = "SphereFBX";
            stringpair.UnityObjectName = "Sphere";
            fbxPrefabScript.NameMapping.Add(stringpair);
            PrefabUtility.ReplacePrefab(cubePrefabInstance, cubePrefabParent);
            string cubePrefabInstancePath = AssetDatabase.GetAssetPath(cubePrefabInstance);

            //Create second FBX
            GameObject cube2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject sphere2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // Change name of Sphere to SphereFBX
            sphere2.transform.name = "SphereFBX";
            sphere2.transform.SetParent(cube2.transform);
            GameObject cylinder2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder2.transform.SetParent(sphere2.transform);

            //export our updated hierarchy to the same file path as the original
            SleepForFileTimestamp();
            // "Import" model to Unity (Exporting modified FBX to Unity to see if the remapping works)
            string fbxFileName = ExportSelectedObjects(Application.dataPath + "/Cube.fbx", cube2);
            AssetDatabase.Refresh();

            // Assert Check Sphere = SphereFBX
            Assert.IsTrue(cubePrefabInstance != null);
            Assert.IsTrue(cubePrefabInstance.GetComponent<MeshFilter>().sharedMesh != null);
            Assert.IsTrue(cubePrefabInstance.transform.GetChild(0).name == "SphereFBX");
            Assert.IsTrue(cubePrefabInstance.transform.GetChild(0).GetComponent<MeshFilter>().sharedMesh != null);

            // Destroy the objects 
            GameObject.DestroyImmediate(cubePrefabInstance);
            //GameObject.DestroyImmediate(cube2);
        }
    }
}

namespace FbxExporters.PerformanceTests {

    class FbxPrefabAutoUpdaterTestPerformance : FbxExporters.UnitTests.ExporterTestBase {
        [Test]
        public void ExpensivePerformanceTest ()
        {
            const int n = 200;
            const int NoUpdateTimeLimit = 500; // milliseconds
            const int OneUpdateTimeLimit = 500; // milliseconds

            var stopwatch = new System.Diagnostics.Stopwatch ();
            stopwatch.Start();

            // Create N fbx models and N/2 prefabs.
            // Each prefab points to an fbx model.
            //
            // Then modify one fbx model. Shouldn't take longer than 1s.
            var hierarchy = CreateGameObject("the_root");
            var baseName = GetRandomFbxFilePath();
            FbxExporters.Editor.ModelExporter.ExportObject(baseName, hierarchy);

            // Create N fbx models by copying files. Import them all at once.
            var names = new string[n];
            names[0] = baseName;
            stopwatch.Reset();
            stopwatch.Start();
            for(int i = 1; i < n; ++i) {
                names[i] = GetRandomFbxFilePath();
                System.IO.File.Copy(names[0], names[i]);
            }
            Debug.Log("Created fbx files in " + stopwatch.ElapsedMilliseconds);

            stopwatch.Reset();
            stopwatch.Start();
            AssetDatabase.Refresh();
            Debug.Log("Imported fbx files in " + stopwatch.ElapsedMilliseconds);

            // Create N/2 prefabs, each one depends on one of the fbx assets.
            // This loop is very slow, which is sad because it's not the point
            // of the test. That's the only reason we halve n.
            stopwatch.Reset();
            stopwatch.Start();
            var fbxFiles = new GameObject[n / 2];
            for(int i = 0; i < n / 2; ++i) {
                fbxFiles[i] = AssetDatabase.LoadMainAssetAtPath(names[i]) as GameObject;
                Assert.IsTrue(fbxFiles[i]);
            }
            Debug.Log("Loaded fbx files in " + stopwatch.ElapsedMilliseconds);

            stopwatch.Reset();
            stopwatch.Start();
            for(int i = 0; i < n / 2; ++i) {
                var instance = CreateGameObject("prefab_" + i);
                Assert.IsTrue(instance);
                var fbxPrefab = instance.AddComponent<FbxPrefab>();
                new FbxPrefabAutoUpdater.FbxPrefabUtility(fbxPrefab).SetSourceModel(fbxFiles[i]);
                PrefabUtility.CreatePrefab(GetRandomPrefabAssetPath(), fbxFiles[i]);
            }
            Debug.Log("Created prefabs in " + stopwatch.ElapsedMilliseconds);

            // Export a new hierarchy and update one fbx file.
            // Make sure we're timing just the assetdatabase refresh by
            // creating a file and then copying it, and not the FbxExporter.
            var newHierarchy = CreateHierarchy();
            var newHierarchyName = GetRandomFbxFilePath();
            FbxExporters.Editor.ModelExporter.ExportObject(newHierarchyName, newHierarchy);
            try {
                UnityEngine.Debug.unityLogger.logEnabled = false;
                stopwatch.Reset ();
                stopwatch.Start ();
                File.Copy(newHierarchyName, names[0], overwrite: true);
                AssetDatabase.Refresh(); // force the update right now.
            } finally {
                UnityEngine.Debug.unityLogger.logEnabled = true;
            }
            Debug.Log("Import (one change) in " + stopwatch.ElapsedMilliseconds);
            Assert.LessOrEqual(stopwatch.ElapsedMilliseconds, NoUpdateTimeLimit);

            // Try what happens when no prefab gets updated.
            try {
                UnityEngine.Debug.unityLogger.logEnabled = false;
                stopwatch.Reset ();
                stopwatch.Start ();
                string newHierarchyFbxFile = GetRandomFbxFilePath();
                File.Copy(names[0], newHierarchyFbxFile);
                AssetDatabase.Refresh(); // force the update right now.
            } finally {
                UnityEngine.Debug.unityLogger.logEnabled = true;
            }
            Debug.Log("Import (no changes) in " + stopwatch.ElapsedMilliseconds);
            Assert.LessOrEqual(stopwatch.ElapsedMilliseconds, OneUpdateTimeLimit);
        }
    }
}
