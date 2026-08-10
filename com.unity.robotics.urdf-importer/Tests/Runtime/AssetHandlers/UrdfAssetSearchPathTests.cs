using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Robotics.UrdfImporter.Tests
{
    /// <summary>
    /// Resolution of assets that are not sitting next to the URDF, through the roots
    /// given to <see cref="UrdfAssetPathHandler.SetSearchPaths"/>.
    /// </summary>
    /// <remarks>
    /// The layouts covered here are the ones a ROS 2 workspace actually produces, so a
    /// regression shows up as "the mesh silently did not load" rather than as an error.
    /// </remarks>
    public class UrdfAssetSearchPathTests
    {
        const string Package = "probe_description";
        const string RelativeMesh = "meshes/probe.stl";

        string root;
        bool previousRuntimeMode;

        [SetUp]
        public void SetUp()
        {
            previousRuntimeMode = RuntimeUrdf.IsRuntimeMode();
            // The fallback is deliberately runtime-only: editor imports go through the
            // AssetDatabase with Assets-relative paths.
            RuntimeUrdf.runtimeModeEnabled = true;

            root = Path.Combine(Path.GetTempPath(), "urdf_search_paths_" + Path.GetRandomFileName());
            Directory.CreateDirectory(root);
            UrdfAssetPathHandler.SetPackageRoot(root);
        }

        [TearDown]
        public void TearDown()
        {
            UrdfAssetPathHandler.SetSearchPaths(null);
            RuntimeUrdf.runtimeModeEnabled = previousRuntimeMode;
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        /// <summary>Create an empty file, making its directory first.</summary>
        static string Place(params string[] parts)
        {
            string path = Path.Combine(parts);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Empty);
            return path;
        }

        static string Resolve(string urdfPath)
        {
            return UrdfAssetPathHandler.GetRelativeAssetPathFromUrdfPath(urdfPath, false);
        }

        [Test]
        public void PackageUri_FoundUnderARootHoldingPackagesSideBySide()
        {
            string searchRoot = Path.Combine(root, "src");
            string expected = Place(searchRoot, Package, "meshes", "probe.stl");
            UrdfAssetPathHandler.SetSearchPaths(new[] { searchRoot });

            Assert.AreEqual(expected.SetSeparatorChar(),
                Resolve($"package://{Package}/{RelativeMesh}"));
        }

        [Test]
        public void PackageUri_FoundUnderAnAmentInstallPrefix()
        {
            // <prefix>/share/<pkg>/...
            string prefix = Path.Combine(root, "install", Package);
            string expected = Place(prefix, "share", Package, "meshes", "probe.stl");
            UrdfAssetPathHandler.SetSearchPaths(new[] { prefix });

            Assert.AreEqual(expected.SetSeparatorChar(),
                Resolve($"package://{Package}/{RelativeMesh}"));
        }

        [Test]
        public void PackageUri_FoundUnderAColconInstallRoot()
        {
            // <install>/<pkg>/share/<pkg>/... — what colcon produces by default
            string install = Path.Combine(root, "install");
            string expected = Place(install, Package, "share", Package, "meshes", "probe.stl");
            UrdfAssetPathHandler.SetSearchPaths(new[] { install });

            Assert.AreEqual(expected.SetSeparatorChar(),
                Resolve($"package://{Package}/{RelativeMesh}"));
        }

        [Test]
        public void PackageUri_FoundWhenTheRootIsThePackageItself()
        {
            string packageDir = Path.Combine(root, "somewhere", Package);
            string expected = Place(packageDir, "meshes", "probe.stl");
            UrdfAssetPathHandler.SetSearchPaths(new[] { packageDir });

            Assert.AreEqual(expected.SetSeparatorChar(),
                Resolve($"package://{Package}/{RelativeMesh}"));
        }

        [Test]
        public void RelativePath_FoundUnderASearchRoot()
        {
            string searchRoot = Path.Combine(root, "assets");
            string expected = Place(searchRoot, "meshes", "probe.stl");
            UrdfAssetPathHandler.SetSearchPaths(new[] { searchRoot });

            Assert.AreEqual(expected.SetSeparatorChar(), Resolve(RelativeMesh));
        }

        [Test]
        public void NextToTheUrdfWins_SearchPathsAreOnlyAFallback()
        {
            // Same relative path present in both places. The one beside the URDF is the
            // one the author meant; the search roots must not shadow it.
            string besideUrdf = Place(root, "meshes", "probe.stl");
            string searchRoot = Path.Combine(root, "elsewhere");
            Place(searchRoot, "meshes", "probe.stl");
            UrdfAssetPathHandler.SetSearchPaths(new[] { searchRoot });

            Assert.AreEqual(besideUrdf.SetSeparatorChar(), Resolve(RelativeMesh));
        }

        [Test]
        public void NotFoundAnywhere_FallsBackToThePackageRootJoin()
        {
            // Unchanged behaviour for a genuinely missing asset: the caller still gets
            // the path it would have got before search paths existed, so the error
            // message keeps pointing at the expected location.
            UrdfAssetPathHandler.SetSearchPaths(new[] { Path.Combine(root, "empty") });

            Assert.AreEqual(Path.Combine(root, $"{Package}/{RelativeMesh}").SetSeparatorChar(),
                Resolve($"package://{Package}/{RelativeMesh}"));
        }

        [Test]
        public void FileUri_IsAbsoluteAndIgnoresSearchPaths()
        {
            string absolute = Place(root, "outside", "probe.stl");
            UrdfAssetPathHandler.SetSearchPaths(new[] { Path.Combine(root, "elsewhere") });

            Assert.AreEqual(absolute.SetSeparatorChar(), Resolve($"file://{absolute}"));
        }

        [Test]
        public void EditorMode_DoesNotConsultSearchPaths()
        {
            string searchRoot = Path.Combine(root, "src");
            Place(searchRoot, Package, "meshes", "probe.stl");
            UrdfAssetPathHandler.SetSearchPaths(new[] { searchRoot });
            RuntimeUrdf.runtimeModeEnabled = false;

            // Editor imports resolve through the AssetDatabase, where an absolute path
            // outside the project would be meaningless.
            Assert.AreEqual(Path.Combine(root, $"{Package}/{RelativeMesh}").SetSeparatorChar(),
                Resolve($"package://{Package}/{RelativeMesh}"));
        }
    }
}
