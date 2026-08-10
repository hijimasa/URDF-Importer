using NUnit.Framework;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Tests
{
    /// <summary>
    /// Colour taken from a URDF &lt;material&gt; must reach the renderer unchanged,
    /// alpha included.
    /// </summary>
    public class UrdfMaterialTests
    {
        GameObject target;
        bool previousRuntimeMode;

        [SetUp]
        public void SetUp()
        {
            previousRuntimeMode = RuntimeUrdf.IsRuntimeMode();
            // In runtime mode CreateMaterial does not go through the AssetDatabase, so
            // each test builds its material instead of picking up one cached by an
            // earlier run under the same name.
            RuntimeUrdf.runtimeModeEnabled = true;
            target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        [TearDown]
        public void TearDown()
        {
            if (target != null)
            {
                Object.DestroyImmediate(target);
            }
            RuntimeUrdf.runtimeModeEnabled = previousRuntimeMode;
        }

        static Link.Visual.Material Describe(string name, double r, double g, double b, double a)
        {
            return new Link.Visual.Material(
                name, new Link.Visual.Material.Color(new[] { r, g, b, a }));
        }

        static void AssertColor(Color expected, Color actual, string what)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-3f, $"{what}: red");
            Assert.AreEqual(expected.g, actual.g, 1e-3f, $"{what}: green");
            Assert.AreEqual(expected.b, actual.b, 1e-3f, $"{what}: blue");
            Assert.AreEqual(expected.a, actual.a, 1e-3f, $"{what}: alpha");
        }

        [Test]
        public void SetUrdfMaterial_AppliesRgbaToRenderer()
        {
            UrdfMaterial.SetUrdfMaterial(target, Describe("probe_opaque", 0.25, 0.5, 0.75, 1.0));

            Renderer renderer = target.GetComponent<Renderer>();
            Assert.IsNotNull(renderer.sharedMaterial, "material was not assigned");
            AssertColor(new Color(0.25f, 0.5f, 0.75f, 1.0f),
                MaterialExtensions.GetMaterialColor(renderer), "opaque colour");
        }

        [Test]
        public void SetUrdfMaterial_KeepsAlphaFromUrdf()
        {
            // A translucent material is the case that silently degrades: dropping alpha
            // still produces a plausible-looking robot, just an opaque one.
            UrdfMaterial.SetUrdfMaterial(target, Describe("probe_translucent", 0.5, 0.5, 0.5, 0.2));

            Renderer renderer = target.GetComponent<Renderer>();
            Assert.AreEqual(0.2f, MaterialExtensions.GetMaterialColor(renderer).a, 1e-3f,
                "alpha from the URDF must survive");
        }

        [Test]
        public void SetUrdfMaterial_AppliesToEveryRendererBelowTheObject()
        {
            // A URDF visual can expand into several renderers (an imported mesh with
            // submeshes, say). All of them belong to that visual.
            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            child.transform.SetParent(target.transform);

            UrdfMaterial.SetUrdfMaterial(target, Describe("probe_children", 0.1, 0.2, 0.3, 0.4));

            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>())
            {
                AssertColor(new Color(0.1f, 0.2f, 0.3f, 0.4f),
                    MaterialExtensions.GetMaterialColor(renderer), renderer.gameObject.name);
            }
        }

        [Test]
        public void SetUrdfMaterial_NullMaterial_LeavesAnAssignedMaterialAlone()
        {
            UrdfMaterial.SetUrdfMaterial(target, Describe("probe_keep", 0.9, 0.8, 0.7, 1.0));
            Material assigned = target.GetComponent<Renderer>().sharedMaterial;

            // A visual without <material> must not clear what the link already had.
            UrdfMaterial.SetUrdfMaterial(target, null);

            Assert.AreSame(assigned, target.GetComponent<Renderer>().sharedMaterial);
        }
    }
}
