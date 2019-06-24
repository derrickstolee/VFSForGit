using NUnit.Framework;
using System.IO;

namespace GVFS.FunctionalTests.Tests.GitCommands.IncludeMode
{
    [TestFixtureSource(typeof(GitRepoTests), nameof(GitRepoTests.ValidateWorkingTree))]
    [Category(Categories.GitCommands)]
    public class IncludeResetMixedTests : ResetMixedTests
    {
        public IncludeResetMixedTests(bool validateWorkingTree) : base(validateWorkingTree)
        {
        }

        public override void SetupForTest()
        {
            base.SetupForTest();
            IncludeTestHelper.SetUpInclude(this.Enlistment, this.ControlGitRepo.RootPath);
        }
    }
}
