using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Formats.Fbx.Exporter;
using UnityEditor.Formats.Fbx.Exporter;
using System.Collections;

namespace FbxExporter.UnitTests
{
    public class ConvertToNestedPrefabTest : ExporterTestBase
    {
        public static IEnumerable PrefabTestCases
        {
            get
            {
                yield return "Prefabs/RegularPrefab.prefab";
                yield return "Prefabs/RegularPrefab_GO.prefab";
                yield return "Prefabs/RegularPrefab_Model.prefab";
                yield return "Prefabs/RegularPrefab_Regular.prefab";
                yield return "Prefabs/RegularPrefab_Variant.prefab";
                yield return "Prefabs/VariantPrefab.prefab";
                yield return "Prefabs/VariantPrefab_GO.prefab";
                yield return "Prefabs/VariantPrefab_Model.prefab";
                yield return "Prefabs/VariantPrefab_Regular.prefab";
                yield return "Prefabs/VariantPrefab_Variant.prefab";
            }
        }

        [Test, TestCaseSource(typeof(ConvertToNestedPrefabTest), "PrefabTestCases")]
        public void TestConversion(string prefabPath)
        {
            prefabPath = FindPathInUnitTests(prefabPath);
            Assert.That(prefabPath, Is.Not.Null);

            // convert in a temporary location, either from the asset or in the scene (or both?)
            // Get a random directory.
            var path = GetRandomFileNamePath(extName: "");

            var go = AssetDatabase.LoadMainAssetAtPath(prefabPath) as GameObject;
            Assert.That(go);

            // Convert it to a prefab
            var prefab = ConvertToNestedPrefab.Convert(go,
                fbxDirectoryFullPath: path, prefabDirectoryFullPath: path);

            Assert.That(prefab);
            AssertSameHierarchy(go, prefab, ignoreRootName: true);

            // check that the hierarchy matches the original
            // check that the components match
            // check that the meshes and materials are now from the fbx
        }

        public static List<string> ChildNames(Transform a) {
            var names = new List<string>();
            foreach(Transform child in a) {
                names.Add(child.name);
            }
            return names;
        }

        [Test]
        public void TestStaticHelpers()
        {
            // Test IncrementFileName
            {
                var tempPath = Path.GetTempPath ();
                var basename = Path.GetFileNameWithoutExtension (Path.GetRandomFileName ());
                basename = basename + "yo"; // add some non-numeric stuff

                var filename1 = basename + ".fbx";
                var filename2 = Path.Combine(tempPath, basename + " 1.fbx");
                Assert.AreEqual (filename2, ConvertToNestedPrefab.IncrementFileName (tempPath, filename1));

                filename1 = basename + " 1.fbx";
                filename2 = Path.Combine(tempPath, basename + " 2.fbx");
                Assert.AreEqual (filename2, ConvertToNestedPrefab.IncrementFileName (tempPath, filename1));

                filename1 = basename + "1.fbx";
                filename2 = Path.Combine(tempPath, basename + "2.fbx");
                Assert.AreEqual (filename2, ConvertToNestedPrefab.IncrementFileName (tempPath, filename1));

                // UNI-25513: bug was that Cube01.fbx => Cube2.fbx
                filename1 = basename + "01.fbx";
                filename2 = Path.Combine(tempPath, basename + "02.fbx");
                Assert.AreEqual (filename2, ConvertToNestedPrefab.IncrementFileName (tempPath, filename1));
            }

            // Test EnforceUniqueNames
            {
                var a = new GameObject("a");
                var b = new GameObject("b");
                var a1 = new GameObject("a");
                var a2 = new GameObject("a");
                ConvertToNestedPrefab.EnforceUniqueNames(new GameObject[] { a, b, a1, a2 });
                Assert.AreEqual("a", a.name);
                Assert.AreEqual("b", b.name);
                Assert.AreEqual("a 1", a1.name);
                Assert.AreEqual("a 2", a2.name);
            }

            // Test GetOrCreateFbxAsset and WillExportFbx
            {
                var a = CreateHierarchy();

                // Test on an object in the scene
                Assert.That(ConvertToNestedPrefab.WillExportFbx(a));
                var aAsset = ConvertToNestedPrefab.GetOrCreateFbxAsset(a, fbxFullPath: GetRandomFbxFilePath());
                Assert.AreNotEqual(a, aAsset);
                AssertSameHierarchy(a, aAsset, ignoreRootName: true);
                Assert.AreEqual(PrefabAssetType.Model, PrefabUtility.GetPrefabAssetType(aAsset));
                Assert.AreEqual(PrefabInstanceStatus.NotAPrefab, PrefabUtility.GetPrefabInstanceStatus(aAsset));

                // Test on an fbx asset
                Assert.That(!ConvertToNestedPrefab.WillExportFbx(aAsset));
                var aAssetAsset = ConvertToNestedPrefab.GetOrCreateFbxAsset(aAsset, fbxFullPath: GetRandomFbxFilePath());
                Assert.AreEqual(aAsset, aAssetAsset);

                // Test on an fbx instance
                var aAssetInstance = PrefabUtility.InstantiatePrefab(aAsset) as GameObject;
                Assert.That(!ConvertToNestedPrefab.WillExportFbx(aAssetInstance));
                var aAssetInstanceAsset = ConvertToNestedPrefab.GetOrCreateFbxAsset(aAssetInstance, fbxFullPath: GetRandomFbxFilePath());
                Assert.AreEqual(aAsset, aAssetInstanceAsset);
            }

            // Test CopyComponents
            {
                var a = GameObject.CreatePrimitive (PrimitiveType.Cube);
                a.name = "a";
                var b = GameObject.CreatePrimitive (PrimitiveType.Sphere);
                b.name = "b";
                a.AddComponent<BoxCollider>();
                a.transform.localPosition += new Vector3(1,2,3);
                Assert.IsFalse(b.GetComponent<BoxCollider>());
                Assert.AreEqual(Vector3.zero, b.transform.localPosition);
                Assert.AreNotEqual (a.GetComponent<MeshFilter>().sharedMesh, b.GetComponent<MeshFilter> ().sharedMesh);
                var nameMap = ConvertToNestedPrefab.MapNameToSourceRecursive(b, a);
                ConvertToNestedPrefab.CopyComponents(b, a, nameMap);
                Assert.IsTrue(b.GetComponent<BoxCollider>());
                Assert.AreEqual(a.transform.localPosition, b.transform.localPosition);
                Assert.AreNotEqual (a.GetComponent<MeshFilter>().sharedMesh, b.GetComponent<MeshFilter> ().sharedMesh);
            }

            // Test UpdateFromSourceRecursive. Very similar but recursive.
            {
                var a = GameObject.CreatePrimitive (PrimitiveType.Cube);
                a.name = "a";
                var a1 = GameObject.CreatePrimitive (PrimitiveType.Cube);
                a1.name = "AA";
                var a2 = GameObject.CreatePrimitive (PrimitiveType.Cube);
                a2.name = "BB";
                a2.transform.parent = a.transform;
                a1.transform.parent = a.transform; // out of alpha order!
                var b = GameObject.CreatePrimitive (PrimitiveType.Sphere);
                b.name = "b";
                var b1 = GameObject.CreatePrimitive (PrimitiveType.Sphere);
                b1.name = "AA";
                var b2 = GameObject.CreatePrimitive (PrimitiveType.Sphere);
                b2.name = "BB";
                b1.transform.parent = b.transform;
                b2.transform.parent = b.transform; // in alpha order
                a.AddComponent<BoxCollider> ();
                a1.transform.localPosition = new Vector3 (1, 2, 3);

                Assert.AreNotEqual(b.GetComponent<MeshFilter>().sharedMesh, a.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsFalse (b.GetComponent<BoxCollider> ());
                Assert.AreEqual ("BB", b.transform.GetChild (1).name);
                Assert.AreEqual (Vector3.zero, b1.transform.localPosition);

                ConvertToNestedPrefab.UpdateFromSourceRecursive (b, a);

                // everything except the mesh + materials should change
                Assert.AreNotEqual(b.GetComponent<MeshFilter>().sharedMesh, a.GetComponent<MeshFilter>().sharedMesh);
                Assert.IsTrue (b.GetComponent<BoxCollider> ());
                Assert.AreEqual ("BB", b.transform.GetChild (1).name);
                Assert.AreEqual (a1.transform.localPosition, b1.transform.localPosition);
            }
        }

        [Test]
        public void BasicTest()
        {
            // Get a random directory.
            var path = GetRandomFileNamePath(extName: "");

            // Create a cube.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // Convert it to a prefab
            var cubePrefab = ConvertToNestedPrefab.Convert(cube,
                fbxDirectoryFullPath: path, prefabDirectoryFullPath: path);

            // Make sure it's what we expect.
            Assert.That(!cube); // original was deleted
            Assert.That(cubePrefab); // we got the new
            Assert.AreEqual("Cube", cubePrefab.name); // it has the right name
            Assert.That(!EditorUtility.IsPersistent(cubePrefab)); // cubePrefab is an instance in the scene

            // Should be all the same triangles. But it isn't. TODO.
            // At least the indices should match in multiplicity.
            var cubeMesh = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshFilter>().sharedMesh;
            var cubePrefabMesh = cubePrefab.GetComponent<MeshFilter>().sharedMesh;
            //Assert.That(
            //  cubeMesh.triangles,
            //  Is.EqualTo(cubePrefabMesh.triangles)
            //);
            Assert.That(cubeMesh.triangles, Is.EquivalentTo(cubeMesh.triangles));

            // Make sure it's where we expect.
            var assetRelativePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(cubePrefab);
            var assetFullPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../" + assetRelativePath));
            Assert.AreEqual(Path.GetFullPath(path), Path.GetDirectoryName(assetFullPath));
        }

        [Test]
        public void ExhaustiveTests() {
            // Try convert in every corner case we can imagine.

            // Test Convert on an object in the scene
            {
                var a = CreateHierarchy();
                var aConvert = ConvertToNestedPrefab.Convert(a, fbxFullPath: GetRandomFbxFilePath(), prefabFullPath: GetRandomPrefabAssetPath());
                // original hierarchy was deleted, recreate
                a = CreateHierarchy();
                AssertSameHierarchy(a, aConvert, ignoreRootName: true);
            }

            // Test Convert on a prefab asset.
            // Expected: creates a new fbx and a new prefab.
            {
                var a = CreateHierarchy();
                var aPrefabPath = GetRandomPrefabAssetPath();
                var bPrefabPath = GetRandomPrefabAssetPath();

                // Convert an existing prefab (by creating a new prefab here).
                var aPrefab = PrefabUtility.SaveAsPrefabAsset(a, aPrefabPath); // PrefabUtility.CreatePrefab(aPrefabPath, a);

                // Provide a different prefab path if convert needs to create a new file.
                var aConvert = ConvertToNestedPrefab.Convert(aPrefab, fbxFullPath: GetRandomFbxFilePath(), prefabFullPath: bPrefabPath);

                // Make sure we exported to the new prefab, didn't change the original
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(aConvert), Is.EqualTo(bPrefabPath));
                Assert.IsTrue(aPrefab);
                Assert.AreNotEqual(aPrefab, aConvert);
            }

            // Test Convert on a prefab instance.
            // Expected: creates a new fbx and new prefab; 'a' points to the new prefab now. Old prefab still exists.
            {
                var a = CreateHierarchy();
                var aPrefabPath = GetRandomPrefabAssetPath();
                var aPrefab = PrefabUtility.SaveAsPrefabAsset(a, aPrefabPath);
                var bPrefabPath = GetRandomPrefabAssetPath();
                var aConvert = ConvertToNestedPrefab.Convert(a, fbxFullPath: GetRandomFbxFilePath(), prefabFullPath: bPrefabPath);
                Assert.AreEqual(bPrefabPath, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(aConvert));
                Assert.AreEqual(aPrefabPath, AssetDatabase.GetAssetPath(aPrefab));
                Assert.AreNotEqual(aPrefabPath, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(aConvert));
                AssertSameHierarchy(aPrefab, aConvert, ignoreRootName: true);
            }

            // Test Convert on an fbx asset
            // Expected: uses the old fbx and creates a new prefab
            {
                var a = CreateHierarchy();
                var aFbx = ExportToFbx(a);
                var aConvert = ConvertToNestedPrefab.Convert(aFbx, fbxFullPath: GetRandomFbxFilePath(), prefabFullPath: GetRandomPrefabAssetPath());
                Assert.AreNotEqual(aFbx, aConvert);
                AssertSameHierarchy(a, aConvert, ignoreRootName: true);
            }

            // Test Convert on an fbx instance
            // Expected: uses the old fbx and creates a new prefab
            {
                var a = CreateHierarchy();
                var aFbx = ExportToFbx(a);
                var aFbxInstance = PrefabUtility.InstantiatePrefab(aFbx) as GameObject;
                var aConvert = ConvertToNestedPrefab.Convert(aFbxInstance, fbxFullPath: GetRandomFbxFilePath(), prefabFullPath: GetRandomPrefabAssetPath());
                Assert.AreNotEqual(aFbx, aConvert);
                AssertSameHierarchy(a, aConvert, ignoreRootName: true);
            }

            // Test Convert on an fbx instance, but not the root.
            // Expected: creates a new fbx and creates a new prefab
            {
                var a = CreateHierarchy();
                var aFbx = ExportToFbx(a);
                var aFbxInstance = PrefabUtility.InstantiatePrefab(aFbx) as GameObject;
                PrefabUtility.UnpackPrefabInstance(aFbxInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var aFbxInstanceChild = aFbxInstance.transform.GetChild(0).gameObject;
                var aConvertFbxPath = GetRandomFbxFilePath();
                var aConvert = ConvertToNestedPrefab.Convert(aFbxInstanceChild, fbxFullPath: aConvertFbxPath, prefabFullPath: GetRandomPrefabAssetPath());
                AssertSameHierarchy(a.transform.GetChild(0).gameObject, aConvert, ignoreRootName: true);
            }
        }

        [Test]
        public void SkinnedMeshTest()
        {
            // Create a cube with a bogus skinned-mesh rather than a static
            // mesh setup.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.AddComponent<SkinnedMeshRenderer>();
            var meshFilter = cube.GetComponent<MeshFilter>();
            var meshRender = cube.GetComponent<MeshRenderer>();
            Object.DestroyImmediate(meshRender);
            Object.DestroyImmediate(meshFilter);

            // Convert it.
            var file = GetRandomFbxFilePath();
            var cubePrefab = ConvertToNestedPrefab.Convert(cube, fbxFullPath: file, prefabFullPath: Path.ChangeExtension(file, ".prefab"));

            // Make sure it has a skinned mesh renderer
            Assert.That(cubePrefab.GetComponentsInChildren<SkinnedMeshRenderer>(), Is.Not.Empty);
        }

        [Test]
        public void MapNameToSourceTest()
        {
            //Create a cube with 3 children game objects
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);

            capsule.transform.parent = cube.transform;
            sphere.transform.parent = cube.transform;
            quad.transform.parent = cube.transform;
            capsule.transform.SetSiblingIndex(0);

            //Create a similar Heirarchy that we can use as our phony "exported" hierarchy.
            var cube2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var capsule2 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var sphere2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var quad2 = GameObject.CreatePrimitive(PrimitiveType.Quad);

            capsule2.transform.parent = cube2.transform;
            sphere2.transform.parent = cube2.transform;
            quad2.transform.parent = cube2.transform;
            capsule.transform.SetSiblingIndex(1);

            var dictionary = ConvertToNestedPrefab.MapNameToSourceRecursive(cube, cube2);

            //We expect these to pass because we've given it an identical game object, as it would have after a normal export.
            Assert.AreSame(capsule2, dictionary[capsule.name]);
            Assert.AreSame(sphere2, dictionary[sphere.name]);
            Assert.AreSame(quad2, dictionary[quad.name]);
            Assert.True(dictionary.Count == 4);

            //Create a broken hierarchy, one that is missing a primitive
            var cube3 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var capsule3 = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var sphere3 = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            capsule3.transform.parent = cube3.transform;
            sphere3.transform.parent = cube3.transform;

            var dictionaryBroken = ConvertToNestedPrefab.MapNameToSourceRecursive(cube, cube3);

            //the dictionary size should be equal to the amount of children + the parent
            Assert.True(dictionaryBroken.Count == 4);

            Assert.IsNull(dictionaryBroken[quad.name]);
            Assert.AreSame(capsule3, dictionaryBroken[capsule.name]);
            Assert.AreSame(sphere3, dictionaryBroken[sphere.name]);
        }

        [Test]
        public void TestInstanceNameMatchesFilename()
        {
            // create a cube, export it to random filename
            // make sure instance name gets updated when converting to prefab

            // Get a random directory.
            var path = GetRandomFileNamePath(extName: ".fbx");

            // Create a cube.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

            // Convert it to a prefab
            var cubePrefab = ConvertToNestedPrefab.Convert(cube,
                fbxFullPath: path, prefabFullPath: Path.ChangeExtension(path, ".prefab"));

            Assert.That (!cube);
            Assert.That (cubePrefab);

            Assert.AreEqual (Path.GetFileNameWithoutExtension (path), cubePrefab.name);
        }
    }
}