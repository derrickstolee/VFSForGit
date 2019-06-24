using NUnit.Framework;

namespace GVFS.FunctionalTests.Tests.GitCommands.IncludeMode
{
    [TestFixtureSource(typeof(GitRepoTests))]
    [Category(Categories.GitCommands)]
    public class IncludeRmTests : RmTests
    {
        public IncludeRmTests() : base()
        {
        }

        public override void SetupForTest()
        {
            base.SetupForTest();
            IncludeTestHelper.SetUpInclude(this.Enlistment, this.ControlGitRepo.RootPath);
        }
    }
}
