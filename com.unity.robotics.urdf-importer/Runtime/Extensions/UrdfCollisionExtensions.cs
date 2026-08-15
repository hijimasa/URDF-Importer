/*
© Siemens AG, 2018
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

namespace Unity.Robotics.UrdfImporter
{
    public static class UrdfCollisionExtensions
    {
        public static UrdfCollision Create(Transform parent, GeometryTypes type, Transform visualToCopy = null)
        {
            GameObject collisionObject = new GameObject(UniqueChildName(parent, null));
            collisionObject.transform.SetParentAndAlign(parent);

            UrdfCollision urdfCollision = collisionObject.AddComponent<UrdfCollision>();
            urdfCollision.geometryType = type;

            if (visualToCopy != null)
            {
                if (urdfCollision.geometryType == GeometryTypes.Mesh)
                {
                    UrdfGeometryCollision.CreateMatchingMeshCollision(collisionObject.transform, visualToCopy);
                }
                else
                {
                    UrdfGeometryCollision.Create(collisionObject.transform, type);
                }

                //copy transform values from corresponding UrdfVisual
                collisionObject.transform.position = visualToCopy.position;
                collisionObject.transform.localScale = visualToCopy.localScale;
                collisionObject.transform.rotation = visualToCopy.rotation;
            }
            else
            {
                UrdfGeometryCollision.Create(collisionObject.transform, type);
            }

#if UNITY_EDITOR
            UnityEditor.EditorGUIUtility.PingObject(collisionObject);
#endif
            return urdfCollision;
        }

        public static UrdfCollision Create(Transform parent, Link.Collision collision)
        {
            GameObject collisionObject = new GameObject(UniqueChildName(parent, null));
            collisionObject.transform.SetParentAndAlign(parent);
            UrdfCollision urdfCollision = collisionObject.AddComponent<UrdfCollision>();
            urdfCollision.geometryType = UrdfGeometry.GetGeometryType(collision.geometry);
            UrdfGeometryCollision.Create(collisionObject.transform, urdfCollision.geometryType, collision.geometry);
            UrdfOrigin.ImportOriginData(collisionObject.transform, collision.origin);
            return urdfCollision;
        }
    
        public static Link.Collision ExportCollisionData(this UrdfCollision urdfCollision)
        {
            UrdfGeometry.CheckForUrdfCompatibility(urdfCollision.transform, urdfCollision.geometryType);

            Link.Geometry geometry = UrdfGeometry.ExportGeometryData(urdfCollision.geometryType, urdfCollision.transform, true);
            string collisionName = urdfCollision.name == "unnamed" ? null : urdfCollision.name;

            return new Link.Collision(geometry, collisionName, UrdfOrigin.ExportOriginData(urdfCollision.transform));
        }
    
        /// <summary>
        /// Make a name that is not already taken by a sibling.
        /// </summary>
        /// <remarks>
        /// URDF makes the name attribute of visual and collision optional, so a link with
        /// several of them ends up with several children all called "unnamed". Duplicate
        /// sibling names make prefab override paths ambiguous - overrides recorded against
        /// one of them can be re-applied to another after a reimport - and they break any
        /// lookup that addresses a part by path. Number them instead.
        /// </remarks>
        private static string UniqueChildName(Transform parent, string desired)
        {
            string baseName = string.IsNullOrEmpty(desired) ? "unnamed" : desired;
            if (parent == null || parent.Find(baseName) == null)
            {
                return baseName;
            }
            for (int i = 1; ; i++)
            {
                string candidate = baseName + "_" + i;
                if (parent.Find(candidate) == null)
                {
                    return candidate;
                }
            }
        }
}
}