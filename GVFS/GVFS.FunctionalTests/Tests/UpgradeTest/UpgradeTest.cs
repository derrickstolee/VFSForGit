using GVFS.FunctionalTests.Tests.EnlistmentPerFixture;
using GVFS.FunctionalTests.Tools;
using GVFS.Tests.Should;
using NUnit.Framework;
using System.Threading;

namespace GVFS.FunctionalTests.Tests.UpgradeTest
{
    /// <summary>
    /// Test `gvfs upgrade` with NuGet upgrade feed. This can be the only test
    /// we run in the entire test suite, because it will replace our installed
    /// version of VFS for Git.
    /// </summary>
    [TestFixture]
    [Category(Categories.Upgrade)]
    public class UpgradeTest
    {
        [Test]
        public void NuGetUpgradeTest()
        {
            // Ensure the service is running, or the upgrade verb will fail
            ProcessHelper.Run("sc", "start GVFS.Service");

            string origVersion = ProcessHelper.Run(GVFSTestConfig.PathToGVFS, "version").Errors;

            this.RunConfigCommand("upgrade.organization 1essharedassets");
            this.RunConfigCommand("upgrade.platform windows");
            this.RunConfigCommand("upgrade.ring test");
            this.RunConfigCommand("upgrade.orgInfoServerUrl https://vfsforgitorginfofunctionapp.azurewebsites.net");
            this.RunConfigCommand("upgrade.feedurl https://pkgs.dev.azure.com/1esSharedAssets/_packaging/GVFS/nuget/v3/index.json");
            this.RunConfigCommand("upgrade.feedpackagename Microsoft.VfsForGitEnvironment");

            ProcessResult result = ProcessHelper.Run(GVFSTestConfig.PathToGVFS, $"upgrade");
            result.ExitCode.ShouldEqual(0, $"Failed to find upgrade with errors: {result.Errors}");

            result = ProcessHelper.Run(GVFSTestConfig.PathToGVFS, $"upgrade --confirm");
            result.ExitCode.ShouldEqual(0, $"Failed to perform upgrade.\nOutput: {result.Output}\nErrors: {result.Errors}");

            // Wait for upgrade to finish... then validate `gvfs version` _changes_
            string newVersion;
            int retryCount = 0;
            do
            {
                Thread.Sleep(10 * 1000);
                ProcessResult versionresult = ProcessHelper.Run(GVFSTestConfig.PathToGVFS, "version");

                if (versionresult.ExitCode == 0)
                {
                    newVersion = versionresult.Errors;
                }
                else
                {
                    newVersion = null;
                }
            }
            while (origVersion == newVersion && retryCount++ < 18);

            origVersion.ShouldNotEqual(newVersion);
        }

        private string RunConfigCommand(string argument, int expectedExitCode = 0)
        {
            ProcessResult result = ProcessHelper.Run(GVFSTestConfig.PathToGVFS, $"config {argument}");
            result.ExitCode.ShouldEqual(expectedExitCode, $"'\"{GVFSTestConfig.PathToGVFS}\" config {argument}' failed with errors: {result.Errors}");

            return result.Output;
        }
    }
}
