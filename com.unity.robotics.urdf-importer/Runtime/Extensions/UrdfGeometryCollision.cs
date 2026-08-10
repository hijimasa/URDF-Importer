/*
© Siemens AG, 2018-2019
Author: Suzannah Smith (suzannah.smith@siemens.com)
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
using System.Collections.Generic;
using MeshProcess;
using System.IO;
using UnityMeshImporter;

namespace Unity.Robotics.UrdfImporter
{
    public class UrdfGeometryCollision : UrdfGeometry
    {
        public static List<string> UsedTemplateFiles => s_UsedTemplateFiles;
        static List<string> s_UsedTemplateFiles = new List<string>();
        static List<string> s_CreatedAssetNames = new List<string>();

        /// <summary>
        /// Identifies a mesh asset by its full path without the extension, normalized to
        /// forward slashes. Keying on the full path keeps meshes that share a file name in
        /// different directories (e.g. visual and collision copies of a link) distinct.
        /// </summary>
        public static string GetAssetKey(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return assetPath;
            }
            string directory = Path.GetDirectoryName(assetPath) ?? "";
            string fileName = Path.GetFileNameWithoutExtension(assetPath);
            return Path.Combine(directory, fileName).Replace('\\', '/');
        }

        public static void Create(Transform parent, GeometryTypes geometryType, Link.Geometry geometry = null)
        {
            GameObject geometryGameObject = null;
            
            switch (geometryType)
            {
                case GeometryTypes.Box:
                    geometryGameObject = new GameObject(geometryType.ToString());
                    geometryGameObject.AddComponent<BoxCollider>();
                    break;
                case GeometryTypes.Cylinder:
                    geometryGameObject = CreateCylinderCollider();
                    break;
                case GeometryTypes.Sphere:
                    geometryGameObject = new GameObject(geometryType.ToString());
                    geometryGameObject.AddComponent<SphereCollider>();
                    break;
                case GeometryTypes.Mesh:
                    if (geometry != null)
                    {
                        geometryGameObject = CreateMeshCollider(geometry.mesh);
                    }
                    else
                    {
                        geometryGameObject = new GameObject(geometryType.ToString());
                        geometryGameObject.AddComponent<MeshCollider>();
                    }
                    break;
            }

            if (geometryGameObject != null)
            {
                geometryGameObject.transform.SetParentAndAlign(parent);
                if (geometry != null)
                {
                    SetScale(parent, geometry, geometryType);
                }
            }
        }

        private static GameObject CreateMeshCollider(Link.Geometry.Mesh mesh)
        {
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                GameObject prefabObject = LocateAssetHandler.FindUrdfAsset<GameObject>(mesh.filename);
                if (prefabObject == null)
                {
                    Debug.LogError("Unable to create mesh collider for the mesh: " + mesh.filename);
                    return null;
                }

                GameObject meshObject = (GameObject)RuntimeUrdf.PrefabUtility_InstantiatePrefab(prefabObject);
                ConvertMeshToColliders(meshObject, location:mesh.filename);

                return meshObject;
            }
            return CreateMeshColliderRuntime(mesh);
        }

        private static GameObject CreateMeshColliderRuntime(Link.Geometry.Mesh mesh)
        {
            GameObject meshObject = null;
            try
            {
                string meshFilePath = UrdfAssetPathHandler.GetRelativeAssetPathFromUrdfPath(mesh.filename, false);
                if (meshFilePath.ToLower().EndsWith(".stl"))
                {
                    meshObject = StlAssetPostProcessor.CreateStlGameObjectRuntime(meshFilePath);
                }
                else if (meshFilePath.ToLower().EndsWith(".obj") || meshFilePath.ToLower().EndsWith(".dae"))
                {
                    meshObject = MeshImporter.Load(meshFilePath);
                }

                if (meshObject != null)
                {
                    ConvertMeshToColliders(meshObject);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogAssertion(ex);
            }

            if (meshObject == null)
            {
                Debug.LogError("Unable to create mesh collider for the mesh: " + mesh.filename);
            }
            return meshObject;
        }

        private static GameObject CreateCylinderCollider()
        {
            GameObject gameObject = new GameObject("Cylinder");
            MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();

            Link.Geometry.Cylinder cylinder = new Link.Geometry.Cylinder(0.5, 2); //Default unity cylinder sizes

            meshFilter.sharedMesh = CreateCylinderMesh(cylinder);
            ConvertCylinderToCollider(meshFilter);

            return gameObject;
        }

        private static void ConvertCylinderToCollider(MeshFilter filter)
        {
            GameObject go = filter.gameObject;
            var collider = filter.sharedMesh;
            // Only create an asset if not runtime import
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                var packageRoot = UrdfAssetPathHandler.GetPackageRoot();
                var filePath = RuntimeUrdf.AssetDatabase_GUIDToAssetPath(RuntimeUrdf.AssetDatabase_CreateFolder($"{packageRoot}", "meshes"));
                var name =$"{filePath}/Cylinder.asset";
                // Only create new asset if one doesn't exist
                if (!RuntimeUrdf.AssetExists(name))
                {
                    Debug.Log($"Creating new cylinder file: {name}");
                    RuntimeUrdf.AssetDatabase_CreateAsset(collider, name, uniquePath:true);
                    RuntimeUrdf.AssetDatabase_SaveAssets();       
                }
                else
                {
                    collider = RuntimeUrdf.AssetDatabase_LoadAssetAtPath<Mesh>(name);
                }
            }
            MeshCollider current = go.AddComponent<MeshCollider>();
            current.sharedMesh = collider;
            current.convex = true;
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(filter);
        }

        public static void CreateMatchingMeshCollision(Transform parent, Transform visualToCopy)
        {
            if (visualToCopy.childCount == 0)
            {
                return;
            }

            GameObject objectToCopy = visualToCopy.GetChild(0).gameObject;
            GameObject prefabObject = (GameObject)RuntimeUrdf.PrefabUtility_GetCorrespondingObjectFromSource(objectToCopy);

            GameObject collisionObject;
            if (prefabObject != null)
            {
                collisionObject = (GameObject)RuntimeUrdf.PrefabUtility_InstantiatePrefab(prefabObject);
            }
            else
            {
                collisionObject = Object.Instantiate(objectToCopy);
            }

            collisionObject.name = objectToCopy.name;
            ConvertMeshToColliders(collisionObject);

            collisionObject.transform.SetParentAndAlign(parent);
        }

        private static void ConvertMeshToColliders(GameObject gameObject, string location = null, bool setConvex = true)
        {
            MeshFilter[] meshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
            if (UrdfRobotExtensions.importsettings.convexMethod == ImportSettings.convexDecomposer.unity)
            {
                foreach (MeshFilter meshFilter in meshFilters)
                {
                    GameObject child = meshFilter.gameObject;
                    MeshCollider meshCollider = child.AddComponent<MeshCollider>();
                    meshCollider.sharedMesh = meshFilter.sharedMesh;

                    meshCollider.convex = setConvex;

                    Object.DestroyImmediate(child.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(meshFilter);
                }
            }
            else
            {
                string templateFileName = "";
                string templateFileKey = "";
                string filePath = "";
                int meshIndex = 0;
                if (!RuntimeUrdf.IsRuntimeMode() && location != null)
                {
                    string meshFilePath = UrdfAssetPathHandler.GetRelativeAssetPathFromUrdfPath(location, false);
                    templateFileName = Path.GetFileNameWithoutExtension(meshFilePath);
                    filePath = Path.GetDirectoryName(meshFilePath);
                    templateFileKey = GetAssetKey(meshFilePath);
                }

                foreach (MeshFilter meshFilter in meshFilters)
                {
                    GameObject child = meshFilter.gameObject;
                    VHACD decomposer = child.AddComponent<VHACD>();
                    List<Mesh> colliderMeshes = null;
                    try
                    {
                        colliderMeshes = decomposer.GenerateConvexMeshes(meshFilter.sharedMesh);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"VHACD failed to decompose the mesh on {child.name}: {e.Message} " +
                                         "Falling back to a convex mesh collider for this mesh.");
                    }

                    if (colliderMeshes == null)
                    {
                        Component.DestroyImmediate(child.GetComponent<VHACD>());
                        MeshCollider fallbackCollider = child.AddComponent<MeshCollider>();
                        fallbackCollider.sharedMesh = meshFilter.sharedMesh;
                        fallbackCollider.convex = setConvex;
                        Object.DestroyImmediate(child.GetComponent<MeshRenderer>());
                        Object.DestroyImmediate(meshFilter);
                        continue;
                    }

                    foreach (Mesh collider in colliderMeshes)
                    {
                        var c = collider;
                        if (!RuntimeUrdf.IsRuntimeMode())
                        {
                            meshIndex++;
                            // "_vhacd_" namespaces the hull assets so they can never collide with
                            // the "{name}_{i}.asset" meshes created when an stl is post processed.
                            string name = $"{filePath}/{templateFileName}_vhacd_{meshIndex}.asset";
                            // Only create new asset if one doesn't exist or should overwrite
                            if ((UrdfRobotExtensions.importsettings.OverwriteExistingPrefabs || !RuntimeUrdf.AssetExists(name)) && !s_CreatedAssetNames.Contains(name))
                            {
                                Debug.Log($"Creating new mesh file: {name}");
                                RuntimeUrdf.AssetDatabase_CreateAsset(c, name);
                                RuntimeUrdf.AssetDatabase_SaveAssets();
                                s_CreatedAssetNames.Add(name);
                                s_UsedTemplateFiles.Add(templateFileKey);
                            }
                            else
                            {
                                c = RuntimeUrdf.AssetDatabase_LoadAssetAtPath<Mesh>(name);
                            }
                        }
                        MeshCollider current = child.AddComponent<MeshCollider>();
                        current.sharedMesh = c;
                        current.convex = setConvex;
                    }
                    Component.DestroyImmediate(child.GetComponent<VHACD>());
                    Object.DestroyImmediate(child.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(meshFilter);
                }
            }
        }

        public static void BeginNewUrdfImport()
        {
            s_CreatedAssetNames.Clear();
            s_UsedTemplateFiles.Clear();
        }

    }
}
