using GVFS.FunctionalTests.Tools;
using System.IO;

namespace GVFS.FunctionalTests.Tests.GitCommands.IncludeMode
{
    public class IncludeTestHelper
    {
        public static void SetUpInclude(GVFSFunctionalTestEnlistment enlistment, string controlRepoRoot, string[] paths = null)
        {
            if (paths == null)
            {
                paths = new[]
                {
                    "CheckoutNewBranchFromStartingPointTest",
                    "CheckoutOrhpanBranchFromStartingPointTest",
                    "DeleteFileWithNameAheadOfDotAndSwitchCommits",
                    "EnumerateAndReadTestFiles",
                    "ErrorWhenPathTreatsFileAsFolderMatchesNTFS",
                    "file.txt", // Changes to a folder in one test
                    "foo.cpp", // Changes to a folder in one test
                    "FilenameEncoding",
                    "GitCommandsTests",
                    Path.Combine("GVFS", "GVFS"),
                    "Scripts",
                    "Test_ConflictTests",
                    "Test_EPF_MoveRenameFileTests",
                    "Test_EPF_MoveRenameFileTests_2",
                    "Test_EPF_MoveRenameFolderTests",
                    "Test_EPF_UpdatePlaceholderTests",
                    "Test_EPF_WorkingDirectoryTests",
                    "TrailingSlashTests",
                };
            }

            using (Stream outStream = File.OpenWrite(Path.Combine(controlRepoRoot, ".git", "info", "sparse-checkout")))
            using (StreamWriter outWriter = new StreamWriter(outStream))
            {
                foreach (string path in paths)
                {
                    new GVFSProcess(enlistment).AddIncludedFolders(path);
                    outWriter.Write($"/{path}\n");
                }

                // sparse-checkout requires all files in root to be declared
                string[] files = new[]
                {
                    ".gitattributes",
                    ".gitignore",
                    "AuthoringTests.md",
                    "GVFS.sln",
                    "Protocol.md",
                    "Readme.md",
                    "Settings.SyleCop",
                };

                foreach (string file in files)
                {
                    outWriter.Write($"/{file}\n");
                }
            }

            GitProcess.Invoke(controlRepoRoot, "config core.sparseCheckout true");
            GitProcess.Invoke(controlRepoRoot, "read-tree -m -u HEAD");
            GitProcess.Invoke(controlRepoRoot, "reset --hard");
        }
    }
}
