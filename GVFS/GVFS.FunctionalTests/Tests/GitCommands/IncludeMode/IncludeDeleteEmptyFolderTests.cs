﻿using NUnit.Framework;

namespace GVFS.FunctionalTests.Tests.GitCommands.IncludeMode
{
    [TestFixtureSource(typeof(GitRepoTests), nameof(GitRepoTests.ValidateWorkingTree))]
    [Category(Categories.GitCommands)]
    public class IncludeDeleteEmptyFolderTests : DeleteEmptyFolderTests
    {
        public IncludeDeleteEmptyFolderTests(bool validateWorkingTree) : base(validateWorkingTree)
        {
        }

        public override void SetupForTest()
        {
            base.SetupForTest();
            IncludeTestHelper.SetUpInclude(this.Enlistment, this.ControlGitRepo.RootPath);
        }
    }
}
