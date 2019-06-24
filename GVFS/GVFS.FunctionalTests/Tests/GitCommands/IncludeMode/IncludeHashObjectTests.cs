using NUnit.Framework;

namespace GVFS.FunctionalTests.Tests.GitCommands.IncludeMode
{
    [TestFixture]
    [Category(Categories.GitCommands)]
    public class IncludeHashObjectTests : HashObjectTests
    {
        public override void SetupForTest()
        {
            base.SetupForTest();
            IncludeTestHelper.SetUpInclude(this.Enlistment, this.ControlGitRepo.RootPath);
        }
    }
}
