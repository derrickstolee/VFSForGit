﻿using GSD.FunctionalTests.FileSystemRunners;
using GSD.FunctionalTests.Should;
using GSD.FunctionalTests.Tools;
using GSD.Tests.Should;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace GSD.FunctionalTests.Tests.EnlistmentPerFixture
{
    [TestFixture]
    [NonParallelizable]
    public class PrefetchVerbTests : TestsWithEnlistmentPerFixture
    {
        private const string PrefetchCommitsAndTreesLock = "prefetch-commits-trees.lock";
        private const string MultiPackIndexLock = "multi-pack-index.lock";
        private const string LsTreeTypeInPathBranchName = "FunctionalTests/20181105_LsTreeTypeInPath";

        private FileSystemRunner fileSystem;

        public PrefetchVerbTests()
        {
            this.fileSystem = new SystemIORunner();
        }

        [TestCase, Order(1)]
        public void PrefetchAllMustBeExplicit()
        {
            this.Enlistment.Prefetch(string.Empty, failOnError: false).ShouldContain("Did you mean to fetch all blobs?");
        }

        [TestCase, Order(2)]
        public void PrefetchSpecificFiles()
        {
            this.ExpectBlobCount(this.Enlistment.Prefetch($"--files {Path.Combine("GSD", "GSD", "Program.cs")}"), 1);
            this.ExpectBlobCount(this.Enlistment.Prefetch($"--files {Path.Combine("GSD", "GSD", "Program.cs")};{Path.Combine("GSD", "GSD.FunctionalTests", "GSD.FunctionalTests.csproj")}"), 2);
        }

        [TestCase, Order(3)]
        public void PrefetchByFileExtension()
        {
            this.ExpectBlobCount(this.Enlistment.Prefetch("--files *.cs"), 199);
            this.ExpectBlobCount(this.Enlistment.Prefetch("--files *.cs;*.csproj"), 208);
        }

        [TestCase, Order(4)]
        public void PrefetchByFileExtensionWithHydrate()
        {
            int expectedCount = 3;
            string output = this.Enlistment.Prefetch("--files *.md --hydrate");
            this.ExpectBlobCount(output, expectedCount);
            output.ShouldContain("Hydrated files:   " + expectedCount);
        }

        [TestCase, Order(5)]
        public void PrefetchByFilesWithHydrateWhoseObjectsAreAlreadyDownloaded()
        {
            int expectedCount = 2;
            string output = this.Enlistment.Prefetch(
                $"--files {Path.Combine("GSD", "GSD", "Program.cs")};{Path.Combine("GSD", "GSD.FunctionalTests", "GSD.FunctionalTests.csproj")} --hydrate");
            this.ExpectBlobCount(output, expectedCount);
            output.ShouldContain("Hydrated files:   " + expectedCount);
            output.ShouldContain("Downloaded:       0");
        }

        [TestCase, Order(6)]
        public void PrefetchFolders()
        {
            this.ExpectBlobCount(this.Enlistment.Prefetch($"--folders {Path.Combine("GSD", "GSD")}"), 17);
            this.ExpectBlobCount(this.Enlistment.Prefetch($"--folders {Path.Combine("GSD", "GSD")};{Path.Combine("GSD", "GSD.FunctionalTests")}"), 65);
        }

        [TestCase, Order(7)]
        public void PrefetchIsAllowedToDoNothing()
        {
            this.ExpectBlobCount(this.Enlistment.Prefetch("--files nonexistent.txt"), 0);
            this.ExpectBlobCount(this.Enlistment.Prefetch("--folders nonexistent_folder"), 0);
        }

        [TestCase, Order(8)]
        public void PrefetchFolderListFromFile()
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), "temp.file");
            File.WriteAllLines(
                tempFilePath,
                new[]
                {
                    "# A comment",
                    " ",
                    "gvfs/",
                    "gvfs/gvfs",
                    "gvfs/"
                });

            this.ExpectBlobCount(this.Enlistment.Prefetch("--folders-list \"" + tempFilePath + "\""), 279);
            File.Delete(tempFilePath);
        }

        [TestCase, Order(9)]
        public void PrefetchAll()
        {
            this.ExpectBlobCount(this.Enlistment.Prefetch("--files *"), 494);
            this.ExpectBlobCount(this.Enlistment.Prefetch($"--folders {Path.DirectorySeparatorChar}"), 494);
        }

        [TestCase, Order(10)]
        public void NoopPrefetch()
        {
            this.ExpectBlobCount(this.Enlistment.Prefetch("--files *"), 494);

            this.Enlistment.Prefetch("--files *").ShouldContain("Nothing new to prefetch.");
        }

        // TODO(Mac): Handle that lock files are not deleted on Mac, they are simply unlocked
        [TestCase, Order(11)]
        [Category(Categories.MacTODO.TestNeedsToLockFile)]
        public void PrefetchCleansUpStalePrefetchLock()
        {
            this.Enlistment.Prefetch("--commits");
            this.PostFetchStepShouldComplete();
            string prefetchCommitsLockFile = Path.Combine(this.Enlistment.GetObjectRoot(this.fileSystem), "pack", PrefetchCommitsAndTreesLock);
            prefetchCommitsLockFile.ShouldNotExistOnDisk(this.fileSystem);
            this.fileSystem.WriteAllText(prefetchCommitsLockFile, this.Enlistment.EnlistmentRoot);
            prefetchCommitsLockFile.ShouldBeAFile(this.fileSystem);

            this.fileSystem
                .EnumerateDirectory(this.Enlistment.GetPackRoot(this.fileSystem))
                .Split()
                .Where(file => string.Equals(Path.GetExtension(file), ".keep", StringComparison.OrdinalIgnoreCase))
                .Count()
                .ShouldEqual(1, "Incorrect number of .keep files in pack directory");

            this.Enlistment.Prefetch("--commits");
            this.PostFetchStepShouldComplete();
            prefetchCommitsLockFile.ShouldNotExistOnDisk(this.fileSystem);
        }

        [TestCase, Order(11)]
        [Category(Categories.MacTODO.TestNeedsToLockFile)]  // PostFetchStepShouldComplete waits for a lock file
        public void PrefetchCleansUpPackDir()
        {
            string multiPackIndexLockFile = Path.Combine(this.Enlistment.GetPackRoot(this.fileSystem), MultiPackIndexLock);
            string oldGitTempFile = Path.Combine(this.Enlistment.GetPackRoot(this.fileSystem), "tmp_midx_XXXX");
            string oldKeepFile = Path.Combine(this.Enlistment.GetPackRoot(this.fileSystem), "prefetch-00000000-HASH.keep");

            this.fileSystem.WriteAllText(multiPackIndexLockFile, this.Enlistment.EnlistmentRoot);
            this.fileSystem.WriteAllText(oldGitTempFile, this.Enlistment.EnlistmentRoot);
            this.fileSystem.WriteAllText(oldKeepFile, this.Enlistment.EnlistmentRoot);

            this.Enlistment.Prefetch("--commits");
            this.Enlistment.PostFetchStep();
            oldGitTempFile.ShouldNotExistOnDisk(this.fileSystem);
            oldKeepFile.ShouldNotExistOnDisk(this.fileSystem);

            this.PostFetchStepShouldComplete();
            multiPackIndexLockFile.ShouldNotExistOnDisk(this.fileSystem);
        }

        [TestCase, Order(12)]
        public void PrefetchFilesFromFileListFile()
        {
            string tempFilePath = Path.Combine(Path.GetTempPath(), "temp.file");
            try
            {
                File.WriteAllLines(
                    tempFilePath,
                    new[]
                    {
                        Path.Combine("GSD", "GSD", "Program.cs"),
                        Path.Combine("GSD", "GSD.FunctionalTests", "GSD.FunctionalTests.csproj")
                    });

                this.ExpectBlobCount(this.Enlistment.Prefetch($"--files-list \"{tempFilePath}\""), 2);
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }

        [TestCase, Order(13)]
        public void PrefetchFilesFromFileListStdIn()
        {
            string input = string.Join(
                Environment.NewLine,
                new[]
                {
                    Path.Combine("GSD", "GSD", "packages.config"),
                    Path.Combine("GSD", "GSD.FunctionalTests", "App.config")
                });

            this.ExpectBlobCount(this.Enlistment.Prefetch("--stdin-files-list", standardInput: input), 2);
        }

        [TestCase, Order(14)]
        public void PrefetchFolderListFromStdin()
        {
            string input = string.Join(
                Environment.NewLine,
                new[]
                {
                    "# A comment",
                    " ",
                    "gvfs/",
                    "gvfs/gvfs",
                    "gvfs/"
                });

            this.ExpectBlobCount(this.Enlistment.Prefetch("--stdin-folders-list", standardInput: input), 279);
        }

        public void PrefetchPathsWithLsTreeTypeInPath()
        {
            ProcessResult checkoutResult = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "checkout " + LsTreeTypeInPathBranchName);

            this.ExpectBlobCount(this.Enlistment.Prefetch("--files *"), 496);
        }

        private void ExpectBlobCount(string output, int expectedCount)
        {
            output.ShouldContain("Matched blobs:    " + expectedCount);
        }

        private void PostFetchStepShouldComplete()
        {
            string objectDir = this.Enlistment.GetObjectRoot(this.fileSystem);
            string objectCacheLock = Path.Combine(objectDir, "git-maintenance-step.lock");

            // Wait first, to hopefully ensure the background thread has
            // started before we check for the lock file.
            do
            {
                Thread.Sleep(500);
            }
            while (this.fileSystem.FileExists(objectCacheLock));

            ProcessResult midxResult = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "multi-pack-index verify --object-dir=\"" + objectDir + "\"");
            midxResult.ExitCode.ShouldEqual(0);

            // A commit graph is not always generated, but if it is, then we want to ensure it is in a good state
            if (this.fileSystem.FileExists(Path.Combine(objectDir, "info", "commit-graphs", "commit-graph-chain")))
            {
                ProcessResult graphResult = GitProcess.InvokeProcess(this.Enlistment.RepoRoot, "commit-graph verify --shallow --object-dir=\"" + objectDir + "\"");
                graphResult.ExitCode.ShouldEqual(0);
            }
        }
    }
}