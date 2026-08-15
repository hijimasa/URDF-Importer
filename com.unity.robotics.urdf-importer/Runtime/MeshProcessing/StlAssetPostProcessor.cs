/*
© Siemens AG, 2017-2019
Author: Dr. Martin Bischoff (martin.bischoff@siemens.com)
Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at
<http://www.apache.org/licenses/LICENSE-2.0>.
Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/ 

using UnityEngine;
using System.Linq;
using System.IO;

namespace Unity.Robotics.UrdfImporter
{
    /// <summary>
    /// Utility functions for processing STL mesh files.
    /// Note that no post processing is done on STL files anymore when they are added to the project
    /// As such StlAssetPostProcessor no longer drives from AssetPostProcessor and the OnPostprocessAllAssets()
    /// function is removed. 
    /// </summary>
    public class StlAssetPostProcessor
    {
        private static Material s_DefaultDiffuse = null;
        // Shared default material written next to the meshes for URP/HDRP, where
        // there is no built-in material asset a prefab can reference.
        private const string DefaultMaterialAssetName = "UrdfDefaultDiffuse.mat";

        public static void PostprocessStlFile(string stlFile)
        {
            var stlFileLowercase = stlFile.ToLower();
            if (stlFileLowercase.StartsWith("assets"))
            {
                Debug.Log($"Detected an stl file at {stlFile} - creating a mesh prefab.");
                CreateStlPrefab(stlFile);
            }
            else if (stlFileLowercase.StartsWith("packages"))
            {
                Debug.Log($"Found an stl file at {stlFile} - " + 
                          "skipping post-processing because it's a Package asset");
            }
            else
            {
                Debug.LogWarning($"Found an stl file at {stlFile} - " + 
                                 "skipping post-processing because we don't know how to handle an asset in this location.");
            }
        }

        private static void CreateStlPrefab(string stlFile)
        {
            GameObject gameObject = CreateStlParent(stlFile);
            if (!gameObject)
            {
                Debug.LogWarning($"Could not create a mesh prefab for {stlFile}");
                return;
            }

            RuntimeUrdf.PrefabUtility_SaveAsPrefabAsset(gameObject, GetPrefabAssetPath(stlFile));
            Object.DestroyImmediate(gameObject);
        }

        /// <summary>Material used at runtime, where nothing can be an asset anyway.</summary>
        private static Material GetDefaultDiffuseMaterial()
        {
            if (!s_DefaultDiffuse)
            {   // Could't use the "Default-Diffuse.mat", either because of HDRP or runtime. so let's create one.
                s_DefaultDiffuse = MaterialExtensions.CreateBasicMaterial();
            }
            return s_DefaultDiffuse;
        }

        /// <summary>
        /// Material for the prefab written next to an STL. It must be an asset: a prefab
        /// cannot serialize a reference to a material that only exists in memory, so
        /// saving one leaves the MeshRenderer with a null material. That shows up later as
        /// a part that renders as "no material" and, once the robot itself is turned into a
        /// prefab, as a part that disappears — the visible material was only ever an
        /// instance override on top of the null stored in the prefab.
        /// </summary>
        private static Material GetPrefabDiffuseMaterial(string stlFile)
        {
#if UNITY_EDITOR
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                // The built-in pipeline has a material asset we can point at.
                if (MaterialExtensions.GetRenderPipelineType() == MaterialExtensions.RenderPipelineType.Standard)
                {
                    Material builtin =
                        RuntimeUrdf.AssetDatabase_GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
                    if (builtin != null)
                    {
                        return builtin;
                    }
                }

                // URP/HDRP have no referenceable built-in material, so keep one asset per
                // mesh folder and reuse it.
                string directory = Path.GetDirectoryName(stlFile);
                string materialPath = string.IsNullOrEmpty(directory)
                    ? DefaultMaterialAssetName
                    : Path.Combine(directory, DefaultMaterialAssetName);

                Material asset = RuntimeUrdf.AssetDatabase_LoadAssetAtPath<Material>(materialPath);
                if (asset == null)
                {
                    asset = MaterialExtensions.CreateBasicMaterial();
                    RuntimeUrdf.AssetDatabase_CreateAsset(asset, materialPath);
                }
                if (asset != null)
                {
                    return asset;
                }
                Debug.LogWarning($"Could not create {materialPath}; the prefab for {stlFile} " +
                                 "will be saved without a material.");
            }
#endif
            return GetDefaultDiffuseMaterial();
        }

        private static GameObject CreateStlParent(string stlFile)
        {
            Mesh[] meshes = StlImporter.ImportMesh(stlFile);
            if (meshes == null)
                return null;

            GameObject parent = new GameObject(Path.GetFileNameWithoutExtension(stlFile));
            Material material = GetPrefabDiffuseMaterial(stlFile);

            for (int i = 0; i < meshes.Length; i++)
            {
                string meshAssetPath = GetMeshAssetPath(stlFile, i);
                RuntimeUrdf.AssetDatabase_CreateAsset(meshes[i], meshAssetPath);
                // Use the mesh we just handed to CreateAsset rather than loading it back.
                // CreateAsset turns that very object into the asset, while
                // LoadAssetAtPath can still return null depending on when the import
                // settles - and a null stored here is saved into the prefab, leaving the
                // MeshFilter permanently empty.
                GameObject gameObject = CreateStlGameObject(
                    Path.GetFileNameWithoutExtension(meshAssetPath), meshes[i], material);
                gameObject.transform.SetParent(parent.transform, false);
            }
            return parent;
        }

        private static GameObject CreateStlGameObject(string name, Mesh mesh, Material material)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (mesh == null)
            {
                Debug.LogWarning($"No mesh for {name}; the prefab will render nothing.");
            }
            return gameObject;
        }
        
        public static string GetMeshAssetPath(string stlFile, int i)
        {
            return stlFile.Substring(0, stlFile.Length - 4) + "_" + i.ToString() + ".asset";
        }

        public static string GetPrefabAssetPath(string stlFile)
        {
            return stlFile.Substring(0, stlFile.Length - 4) + ".prefab";
        }
        
        public static GameObject CreateStlGameObjectRuntime(string stlFile)
        {
            Mesh[] meshes = StlImporter.ImportMesh(stlFile);
            if (meshes == null)
            {
                return null;
            }
            
            GameObject parent = new GameObject(Path.GetFileNameWithoutExtension(stlFile));

            Material material = GetDefaultDiffuseMaterial();
            
            for (int i = 0; i < meshes.Length; i++)
            {
                GameObject gameObject = new GameObject(Path.GetFileNameWithoutExtension(GetMeshAssetPath(stlFile, i)));
                gameObject.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                gameObject.transform.SetParent(parent.transform, false);
            }
            return parent;
        }

    }
}