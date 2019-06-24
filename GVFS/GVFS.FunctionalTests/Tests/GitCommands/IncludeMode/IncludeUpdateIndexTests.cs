using NUnit.Framework;

namespace GVFS.FunctionalTests.Tests.GitCommands.IncludeMode
{
    [TestFixtureSource(typeof(GitRepoTests), nameof(GitRepoTests.ValidateWorkingTree))]
    [Category(Categories.GitCommands)]
    public class IncludeUpdateIndexTests : UpdateIndexTests
    {
        public IncludeUpdateIndexTests(bool validateWorkingTree) : base(validateWorkingTree)
        {
        }

        public override void SetupForTest()
        {
            base.SetupForTest();
            IncludeTestHelper.SetUpInclude(this.Enlistment, this.ControlGitRepo.RootPath);
        }
    }
}
