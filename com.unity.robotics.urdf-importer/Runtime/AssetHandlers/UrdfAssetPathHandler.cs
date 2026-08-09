/*
© Siemens AG, 2017-2018
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

using System.IO;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Unity.Robotics.UrdfImporter
{
    public static class UrdfAssetPathHandler
    {
        //Relative to Assets folder
        private static string packageRoot;
        private const string MaterialFolderName = "Materials";

        // Extra roots to look under when an asset cannot be found relative to the
        // URDF file. See SetSearchPaths.
        private static readonly List<string> searchPaths = new List<string>();
        private static string[] amentPrefixes;

        #region SetAssetRootFolder
        public static void SetPackageRoot(string newPath, bool correctingIncorrectPackageRoot = false)
        {
            string oldPackagePath = packageRoot;

            packageRoot = GetRelativeAssetPath(newPath);

            if (!RuntimeUrdf.AssetDatabase_IsValidFolder(Path.Combine(packageRoot, MaterialFolderName)))
            {
                RuntimeUrdf.AssetDatabase_CreateFolder(packageRoot, MaterialFolderName);
            }

            if (correctingIncorrectPackageRoot)
            {
                MoveMaterialsToNewLocation(oldPackagePath);
            }
        }
        #endregion

        #region SearchPaths
        /// <summary>
        /// Roots to search when an asset is not found relative to the URDF file.
        /// </summary>
        /// <remarks>
        /// Mesh references are normally resolved against the directory the URDF was
        /// loaded from, which is fine when the file sits next to its meshes. It is not
        /// enough when the URDF did not come from a file at all — a description passed
        /// as a string, for instance — or when it uses `package://` to name a package
        /// that lives somewhere else entirely.
        ///
        /// Other simulators solve this with a global search path (Gazebo's
        /// GZ_SIM_RESOURCE_PATH) rather than by anchoring to the document. These roots
        /// are the equivalent: they are consulted only after the normal resolution
        /// fails, so nothing changes for URDFs that already load correctly.
        /// </remarks>
        public static void SetSearchPaths(IEnumerable<string> paths)
        {
            searchPaths.Clear();
            if (paths == null)
            {
                return;
            }
            foreach (string path in paths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    searchPaths.Add(path.SetSeparatorChar());
                }
            }
        }

        public static IReadOnlyList<string> GetSearchPaths()
        {
            return searchPaths;
        }

        private static IEnumerable<string> EnumerateSearchRoots()
        {
            foreach (string path in searchPaths)
            {
                yield return path;
            }

            // A sourced ROS 2 environment already lists every install prefix here, so
            // honouring it makes `package://` work with no extra configuration.
            if (amentPrefixes == null)
            {
                string env = Environment.GetEnvironmentVariable("AMENT_PREFIX_PATH");
                amentPrefixes = string.IsNullOrEmpty(env)
                    ? new string[0]
                    : env.Split(Path.PathSeparator);
            }
            foreach (string prefix in amentPrefixes)
            {
                if (!string.IsNullOrEmpty(prefix))
                {
                    yield return prefix;
                }
            }
        }

        /// <summary>
        /// Look for an asset under the configured search roots. Returns an absolute
        /// path to an existing file, or null.
        /// </summary>
        private static string LocateInSearchPaths(string relativePath, bool isPackageUri)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return null;
            }

            string package = null;
            string rest = null;
            if (isPackageUri)
            {
                int separator = relativePath.IndexOfAny(new[] { '/', '\\' });
                if (separator > 0 && separator < relativePath.Length - 1)
                {
                    package = relativePath.Substring(0, separator);
                    rest = relativePath.Substring(separator + 1);
                }
            }

            foreach (string root in EnumerateSearchRoots())
            {
                // The root holds packages side by side, e.g. <ws>/src or a share dir.
                string candidate = Path.Combine(root, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate.SetSeparatorChar();
                }

                if (package == null)
                {
                    continue;
                }

                // An ament install prefix: <prefix>/share/<pkg>/...
                candidate = Path.Combine(root, "share", package, rest);
                if (File.Exists(candidate))
                {
                    return candidate.SetSeparatorChar();
                }

                // A colcon install root with isolated prefixes:
                // <install>/<pkg>/share/<pkg>/...
                candidate = Path.Combine(root, package, "share", package, rest);
                if (File.Exists(candidate))
                {
                    return candidate.SetSeparatorChar();
                }

                // The root is already that package's directory.
                candidate = Path.Combine(root, rest);
                if (File.Exists(candidate))
                {
                    return candidate.SetSeparatorChar();
                }
            }

            return null;
        }
        #endregion

        #region GetPaths
        public static string GetPackageRoot()
        {
            return packageRoot;
        }
        
        public static string GetRelativeAssetPath(string absolutePath)
        {
            string assetPath = absolutePath;
            var absolutePathUnityFormat = absolutePath.SetSeparatorChar();
            if (!absolutePathUnityFormat.StartsWith(Application.dataPath.SetSeparatorChar()))
            {
#if UNITY_EDITOR
                if (!RuntimeUrdf.IsRuntimeMode())
                {
                    if (absolutePath.Length > Application.dataPath.Length)
                    {
                        assetPath = absolutePath.Substring(Application.dataPath.Length - "Assets".Length);
                    }
                }
#endif
            }
            else 
            {
                assetPath = "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            return assetPath.SetSeparatorChar();
        }

        public static string GetFullAssetPath(string relativePath)
        {
            string fullPath = Application.dataPath;
            if (relativePath.Substring(0, "Assets".Length) == "Assets")
            {
                fullPath += relativePath.Substring("Assets".Length);
            }
            else 
            {
                fullPath = fullPath.Substring(0, fullPath.Length - "Assets".Length) + relativePath;
            }
            return fullPath.SetSeparatorChar();
        }

        public static string GetRelativeAssetPathFromUrdfPath(string urdfPath, bool convertToPrefab=true)
        {
            string path;
            bool useFileUri = false;
            bool isPackageUri = false;
            if (!urdfPath.StartsWith(@"file://") && !urdfPath.StartsWith(@"package://"))
            {
               if (urdfPath.Substring(0, 3) == "../")
                {
                   UnityEngine.Debug.LogWarning("Attempting to replace file path's starting instance of `../` with standard package notation `package://` to prevent manual path traversal at root of directory!");
                   urdfPath = $@"package://{urdfPath.Substring(3)}";
                }
            }
            // loading assets relative path from ROS/ROS2 package.
            if (urdfPath.StartsWith(@"package://"))
            {
                path = urdfPath.Substring(10).SetSeparatorChar();
                isPackageUri = true;
            }
            // loading assets from file:// type URI.
            else if (urdfPath.StartsWith(@"file://"))
            {
                path = urdfPath.Substring(7).SetSeparatorChar();
                useFileUri = true;
            }
            else
            {
                path = urdfPath.SetSeparatorChar();
            }

            if (convertToPrefab) 
            {
                if (Path.GetExtension(path)?.ToLowerInvariant() == ".stl")
                    path = path.Substring(0, path.Length - 3) + "prefab";

            }
            if (useFileUri) {
                return path;
            }

            string relativeToPackageRoot = Path.Combine(packageRoot, path);

            // Fall back to the search roots only when the normal resolution does not
            // point at a real file. Editor imports go through the AssetDatabase with
            // Assets-relative paths, so this is limited to runtime loading, where the
            // returned path is opened directly.
            if (RuntimeUrdf.IsRuntimeMode() && !File.Exists(relativeToPackageRoot))
            {
                string located = LocateInSearchPaths(path, isPackageUri);
                if (located != null)
                {
                    return located;
                }
            }

            return relativeToPackageRoot;
        }
        #endregion

        public static bool IsValidAssetPath(string path)
        {
#if UNITY_EDITOR
            if (!RuntimeUrdf.IsRuntimeMode())
            {
                return Directory.Exists(path) || File.Exists(path);
            }
#endif
            //RuntimeImporter. TODO: check if the path really exists
            return true;
        }

        #region Materials

        private static void MoveMaterialsToNewLocation(string oldPackageRoot)
        {
            if (RuntimeUrdf.AssetDatabase_IsValidFolder(Path.Combine(oldPackageRoot, MaterialFolderName)))
            {
                RuntimeUrdf.AssetDatabase_MoveAsset(
                    Path.Combine(oldPackageRoot, MaterialFolderName),
                    Path.Combine(UrdfAssetPathHandler.GetPackageRoot(), MaterialFolderName));
            }
            else
            {
                RuntimeUrdf.AssetDatabase_CreateFolder(UrdfAssetPathHandler.GetPackageRoot(), MaterialFolderName);
            }
        }

        public static string GetMaterialAssetPath(string materialName)
        {
            return Path.Combine(packageRoot, MaterialFolderName, Path.GetFileName(materialName) + ".mat");
        }

        #endregion
    }

}