﻿using GSD.Common;
using GSD.Common.FileSystem;
using GSD.Common.Git;
using GSD.Common.Tracing;
using GSD.UnitTests.Mock.FileSystem;
using GSD.UnitTests.Mock.Git;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;

namespace GSD.UnitTests.Mock.Common
{
    public class MockPlatform : GSDPlatform
    {
        public MockPlatform() : base(underConstruction: new UnderConstructionFlags())
        {
        }

        public string MockCurrentUser { get; set; }

        public override IGitInstallation GitInstallation { get; } = new MockGitInstallation();

        public override IDiskLayoutUpgradeData DiskLayoutUpgrade => throw new NotSupportedException();

        public override IPlatformFileSystem FileSystem { get; } = new MockPlatformFileSystem();

        public override string Name { get => "Mock"; }

        public override string GSDConfigPath { get => Path.Combine("mock:", LocalGSDConfig.FileName); }

        public override GSDPlatformConstants Constants { get; } = new MockPlatformConstants();

        public HashSet<int> ActiveProcesses { get; } = new HashSet<int>();

        public override void ConfigureVisualStudio(string gitBinPath, ITracer tracer)
        {
            throw new NotSupportedException();
        }

        public override bool TryGetGSDHooksPathAndVersion(out string hooksPaths, out string hooksVersion, out string error)
        {
            throw new NotSupportedException();
        }

        public override bool TryInstallGitCommandHooks(GSDContext context, string executingDirectory, string hookName, string commandHookPath, out string errorMessage)
        {
            throw new NotSupportedException();
        }

        public override bool TryVerifyAuthenticodeSignature(string path, out string subject, out string issuer, out string error)
        {
            throw new NotImplementedException();
        }

        public override string GetNamedPipeName(string enlistmentRoot)
        {
            return "GSD_Mock_PipeName";
        }

        public override string GetGSDServiceNamedPipeName(string serviceName)
        {
            return Path.Combine("GSD_Mock_ServicePipeName", serviceName);
        }

        public override NamedPipeServerStream CreatePipeByName(string pipeName)
        {
            throw new NotSupportedException();
        }

        public override string GetCurrentUser()
        {
            return this.MockCurrentUser;
        }

        public override string GetUserIdFromLoginSessionId(int sessionId, ITracer tracer)
        {
            return sessionId.ToString();
        }

        public override string GetOSVersionInformation()
        {
            throw new NotSupportedException();
        }

        public override string GetDataRootForGSD()
        {
            // TODO: Update this method to return non existant file path.
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "GSD");
        }

        public override string GetDataRootForGSDComponent(string componentName)
        {
            return Path.Combine(this.GetDataRootForGSD(), componentName);
        }

        public override Dictionary<string, string> GetPhysicalDiskInfo(string path, bool sizeStatsOnly)
        {
            return new Dictionary<string, string>();
        }

        public override string GetUpgradeProtectedDataDirectory()
        {
            return this.GetDataRootForGSDComponent(ProductUpgraderInfo.UpgradeDirectoryName);
        }

        public override string GetUpgradeLogDirectoryParentDirectory()
        {
            return this.GetUpgradeProtectedDataDirectory();
        }

        public override string GetUpgradeHighestAvailableVersionDirectory()
        {
            return this.GetUpgradeProtectedDataDirectory();
        }

        public override void InitializeEnlistmentACLs(string enlistmentPath)
        {
            throw new NotSupportedException();
        }

        public override bool IsConsoleOutputRedirectedToFile()
        {
            throw new NotSupportedException();
        }

        public override bool IsElevated()
        {
            throw new NotSupportedException();
        }

        public override bool IsProcessActive(int processId)
        {
            return this.ActiveProcesses.Contains(processId);
        }

        public override void IsServiceInstalledAndRunning(string name, out bool installed, out bool running)
        {
            throw new NotSupportedException();
        }

        public override bool TryGetGSDEnlistmentRoot(string directory, out string enlistmentRoot, out string errorMessage)
        {
            throw new NotSupportedException();
        }

        public override bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError)
        {
            throw new NotImplementedException();
        }

        public override void StartBackgroundVFS4GProcess(ITracer tracer, string programName, string[] args)
        {
            throw new NotSupportedException();
        }

        public override void PrepareProcessToRunInBackground()
        {
            throw new NotSupportedException();
        }

        public override bool IsGitStatusCacheSupported()
        {
            return true;
        }

        public override FileBasedLock CreateFileBasedLock(PhysicalFileSystem fileSystem, ITracer tracer, string lockPath)
        {
            return new MockFileBasedLock(fileSystem, tracer, lockPath);
        }

        public override ProductUpgraderPlatformStrategy CreateProductUpgraderPlatformInteractions(
            PhysicalFileSystem fileSystem,
            ITracer tracer)
        {
            return new MockProductUpgraderPlatformStrategy(fileSystem, tracer);
        }

        public override bool TryKillProcessTree(int processId, out int exitCode, out string error)
        {
            error = null;
            exitCode = 0;
            return true;
        }

        public class MockPlatformConstants : GSDPlatformConstants
        {
            public override string ExecutableExtension
            {
                get { return ".mockexe"; }
            }

            public override string InstallerExtension
            {
                get { return ".mockexe"; }
            }

            public override string WorkingDirectoryBackingRootPath
            {
                get { return GSDConstants.WorkingDirectoryRootName; }
            }

            public override string DotGSDRoot
            {
                get { return ".mockGSD"; }
            }

            public override string GSDBinDirectoryPath
            {
                get { return Path.Combine("MockProgramFiles", this.GSDBinDirectoryName); }
            }

            public override string GSDBinDirectoryName
            {
                get { return "MockGSD"; }
            }

            public override string GSDExecutableName
            {
                get { return "MockGSD" + this.ExecutableExtension; }
            }

            public override string ProgramLocaterCommand
            {
                get { return "MockWhere"; }
            }

            public override HashSet<string> UpgradeBlockingProcesses
            {
                get { return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GSD", "GSD.Mount", "git", "wish", "bash" }; }
            }

            public override bool SupportsUpgradeWhileRunning => false;
        }
    }
}
