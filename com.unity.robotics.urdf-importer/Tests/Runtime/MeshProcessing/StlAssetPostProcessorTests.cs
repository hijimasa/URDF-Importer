using System.IO;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Tests
{
    public class StlAssetPostProcessorTests
    {
        const string k_AssetRoot = "Assets/Tests/Runtime/StlAssetPostProcessorTests";
        const string k_StlCubeSourcePath = "Packages/com.unity.robotics.urdf-importer/Tests/Runtime/Assets/URDF/cube/meshes/cube.stl";
        string m_StlCubeCopyPath;

        [SetUp]
        public void SetUp()
        {
            m_StlCubeCopyPath = k_AssetRoot + "/cube.stl";
            RuntimeUrdf.SetRuntimeMode(false);
            Directory.CreateDirectory(k_AssetRoot);
        }
        
        [Test]
        public void StlPostprocess_NewStl_DontCreatePrefab()
        {
            // make a new copy of the stl file
            Assert.IsTrue(AssetDatabase.CopyAsset(k_StlCubeSourcePath, m_StlCubeCopyPath));
            Assert.IsTrue(RuntimeUrdf.AssetExists(m_StlCubeCopyPath));

            // make sure the .asset file is not automatically created
            var meshAssetPath =  StlAssetPostProcessor.GetMeshAssetPath(m_StlCubeCopyPath, 0);            
            Assert.IsFalse(RuntimeUrdf.AssetExists(meshAssetPath));

            // make sure the .prefab file is not automatically created
            var prefabPath = StlAssetPostProcessor.GetPrefabAssetPath(m_StlCubeCopyPath);
            Assert.IsFalse(RuntimeUrdf.AssetExists(prefabPath));
            
            // make sure the .asset and .prefab file are created when requested
            StlAssetPostProcessor.PostprocessStlFile(m_StlCubeCopyPath);
            Assert.IsTrue(RuntimeUrdf.AssetExists(meshAssetPath));
            Assert.IsTrue(RuntimeUrdf.AssetExists(prefabPath));
        }
        
        [Test]
        public void CreateStlGameObjectRuntime_AssignsAMaterialToEveryRenderer()
        {
            // In a built player the UNITY_EDITOR branch of GetDefaultDiffuseMaterial is
            // compiled out, so the cached material starts null and has to be created on
            // demand. When that fell through, renderers were left with a null material and
            // Unity drew them in the magenta "missing shader" colour.
            //
            // The cache is a private static, and an earlier test in the run may already have
            // filled it - which would hide the very fall-through this test is here to catch.
            // Clear it so we exercise the cold path.
            typeof(StlAssetPostProcessor)
                .GetField("s_DefaultDiffuse", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, null);

            RuntimeUrdf.SetRuntimeMode(true);
            try
            {
                Assert.IsTrue(AssetDatabase.CopyAsset(k_StlCubeSourcePath, m_StlCubeCopyPath));
                var stlPath = Path.GetFullPath(m_StlCubeCopyPath);
                var gameObject = StlAssetPostProcessor.CreateStlGameObjectRuntime(stlPath);

                Assert.IsNotNull(gameObject, $"failed to load {stlPath}");
                var renderers = gameObject.GetComponentsInChildren<MeshRenderer>();
                Assert.IsNotEmpty(renderers);
                foreach (var renderer in renderers)
                {
                    Assert.IsNotNull(renderer.sharedMaterial,
                        $"{renderer.name} has no material, so it renders magenta");
                }

                Object.DestroyImmediate(gameObject);
            }
            finally
            {
                RuntimeUrdf.SetRuntimeMode(false);
            }
        }

        [Test]
        public void StlPostprocess_PrefabKeepsMeshAndMaterial()
        {
            // The prefab is the source of truth for anything instantiated from it later.
            // If its MeshFilter/MeshRenderer are saved empty, the part only looks right
            // while the instance override survives - turning the robot into a prefab drops
            // the override and the part disappears. A material that exists only in memory
            // cannot be serialised into a prefab, so it has to be an asset.
            Assert.IsTrue(AssetDatabase.CopyAsset(k_StlCubeSourcePath, m_StlCubeCopyPath));
            StlAssetPostProcessor.PostprocessStlFile(m_StlCubeCopyPath);

            var prefabPath = StlAssetPostProcessor.GetPrefabAssetPath(m_StlCubeCopyPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab, $"no prefab at {prefabPath}");

            var filters = prefab.GetComponentsInChildren<MeshFilter>();
            Assert.IsNotEmpty(filters);
            foreach (var filter in filters)
            {
                Assert.IsNotNull(filter.sharedMesh, $"{filter.name} has no mesh in the prefab");
                var renderer = filter.GetComponent<MeshRenderer>();
                Assert.IsNotNull(renderer.sharedMaterial, $"{renderer.name} has no material in the prefab");
                Assert.IsTrue(AssetDatabase.Contains(renderer.sharedMaterial),
                    $"{renderer.name}'s material is not an asset, so it cannot be stored in a prefab");
            }
        }

        [TearDown]
        public void TearDown()
        {
            List<string> outFailedPaths = new List<string>();
            AssetDatabase.DeleteAssets(new string[] {"Assets/Tests"}, outFailedPaths);
        }
    }
}