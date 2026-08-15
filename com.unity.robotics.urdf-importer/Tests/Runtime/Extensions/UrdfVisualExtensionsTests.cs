using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Geometry = Unity.Robotics.UrdfImporter.Link.Geometry;
using Box = Unity.Robotics.UrdfImporter.Link.Geometry.Box;
using Visual = Unity.Robotics.UrdfImporter.Link.Visual;

namespace Unity.Robotics.UrdfImporter.Tests
{
    public class UrdfVisualExtensionsTests
    {
        [Test]
        public void Create_SeveralUnnamedVisuals_SiblingNamesAreUnique()
        {
            // name is optional on <visual>, so a link can easily end up with several of
            // them and no names at all. Siblings that share a name make prefab override
            // paths ambiguous and break any lookup that addresses a part by path.
            RuntimeUrdf.runtimeModeEnabled = false;
            var parent = new GameObject("Parent").transform;

            for (int i = 0; i < 3; i++)
            {
                UrdfVisualExtensions.Create(parent, new Visual(new Geometry(new Box(new double[] { 1, 1, 1 }))));
            }

            AssertChildNamesAreUnique(parent, 3);
            Object.DestroyImmediate(parent.gameObject);
        }

        [Test]
        public void Create_VisualsSharingAName_SiblingNamesAreUnique()
        {
            // URDF does not require visual names to be unique within a link either.
            RuntimeUrdf.runtimeModeEnabled = false;
            var parent = new GameObject("Parent").transform;

            for (int i = 0; i < 2; i++)
            {
                UrdfVisualExtensions.Create(
                    parent, new Visual(new Geometry(new Box(new double[] { 1, 1, 1 })), "shell"));
            }

            AssertChildNamesAreUnique(parent, 2);
            Assert.IsNotNull(parent.Find("shell"), "the first visual should keep the name from the URDF");
            Object.DestroyImmediate(parent.gameObject);
        }

        [Test]
        public void Create_SeveralUnnamedCollisions_SiblingNamesAreUnique()
        {
            RuntimeUrdf.runtimeModeEnabled = false;
            var parent = new GameObject("Parent").transform;

            for (int i = 0; i < 3; i++)
            {
                UrdfCollisionExtensions.Create(parent, GeometryTypes.Box);
            }

            AssertChildNamesAreUnique(parent, 3);
            Object.DestroyImmediate(parent.gameObject);
        }

        private static void AssertChildNamesAreUnique(Transform parent, int expectedCount)
        {
            Assert.AreEqual(expectedCount, parent.childCount);
            var seen = new HashSet<string>();
            for (int i = 0; i < parent.childCount; i++)
            {
                string name = parent.GetChild(i).name;
                Assert.IsTrue(seen.Add(name), $"'{name}' is used by more than one sibling");
            }
        }
    }
}
