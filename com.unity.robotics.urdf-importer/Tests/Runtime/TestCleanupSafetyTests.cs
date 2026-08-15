using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Unity.Robotics.UrdfImporter.Tests
{
    /// <summary>
    /// Guards against a fixture cleaning up more than it created.
    /// </summary>
    /// <remarks>
    /// These tests run inside whatever project installs the package, so anything they
    /// delete is that project's data. Several fixtures used to hand "Assets/Tests" to
    /// AssetDatabase.DeleteAssets even though each of them only ever wrote to its own
    /// subfolder underneath it. Running the suite in a project that keeps its own tests
    /// there deleted them - which is exactly what happened once. The damage is silent:
    /// the suite still reports green, because the assets it removed were not its own.
    /// </remarks>
    public class TestCleanupSafetyTests
    {
        // Folders a project is likely to own. A fixture that wants a scratch area has to
        // name its own subfolder, e.g. "Assets/Tests/Runtime/&lt;fixture&gt;".
        static readonly string[] k_SharedFolders =
        {
            "\"Assets\"",
            "\"Assets/\"",
            "\"Assets/Tests\"",
            "\"Assets/Tests/\"",
            "\"Assets/meshes\"",
            "\"Assets/Resources\"",
        };

        [Test]
        public void Fixtures_DoNotDeleteFoldersTheProjectOwns()
        {
            string testsRoot = FindTestsRoot();
            Assert.IsTrue(Directory.Exists(testsRoot), $"could not locate the test sources at {testsRoot}");

            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(testsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(file);
                // Only look at the arguments of a delete call, so a folder named in a
                // comment or in this fixture's own list does not count.
                foreach (Match call in Regex.Matches(source, @"DeleteAssets?\s*\([^;]*;"))
                {
                    foreach (string shared in k_SharedFolders)
                    {
                        if (call.Value.Contains(shared))
                        {
                            offenders.Add($"{Path.GetFileName(file)} deletes {shared.Trim('"')}");
                        }
                    }
                }
            }

            Assert.IsEmpty(offenders,
                "A fixture may only delete the folder it created:\n  "
                + string.Join("\n  ", offenders.Distinct()));
        }

        /// <summary>
        /// Directory holding the package's test sources. Uses the compile-time path of
        /// this file so it works whether the package is embedded, local or from git.
        /// </summary>
        static string FindTestsRoot([CallerFilePath] string sourcePath = "")
        {
            // <package>/Tests/Runtime/TestCleanupSafetyTests.cs -> <package>/Tests
            string fromSource = Path.GetDirectoryName(Path.GetDirectoryName(sourcePath));
            if (!string.IsNullOrEmpty(fromSource) && Directory.Exists(fromSource))
            {
                return fromSource;
            }
            return Path.GetFullPath("Packages/com.unity.robotics.urdf-importer/Tests");
        }
    }
}
