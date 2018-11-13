using GVFS.Common.Actions;
using GVFS.Tests.Should;
using GVFS.UnitTests.Mock.FileSystem;
using GVFS.UnitTests.Virtual;
using NUnit.Framework;
using System.Threading;

namespace GVFS.UnitTests.Actions
{
    [TestFixture]
    public class ActionRunnerTests
    {
        [TestCase]
        public void LaunchThreadSucceeds()
        {
            using (CommonRepoSetup setup = new CommonRepoSetup())
            using (ActionRunner prefetcher = new ActionRunner(
                                                    setup.Context.Tracer,
                                                    setup.Context.Enlistment,
                                                    setup.Context.FileSystem,
                                                    setup.GitObjects))
            {
                prefetcher.LaunchThreadIfIdle().ShouldBeTrue();
                prefetcher.WaitForActionsToFinish();
            }
        }

        [TestCase]
        public void RestartThreadSucceeds()
        {
            using (CommonRepoSetup setup = new CommonRepoSetup())
            using (ActionRunner prefetcher = new ActionRunner(
                                                    setup.Context.Tracer,
                                                    setup.Context.Enlistment,
                                                    setup.Context.FileSystem,
                                                    setup.GitObjects))
            {
                prefetcher.LaunchThreadIfIdle().ShouldBeTrue();
                prefetcher.WaitForActionsToFinish();

                prefetcher.LaunchThreadIfIdle().ShouldBeTrue();
                prefetcher.WaitForActionsToFinish();
            }
        }

        [TestCase]
        public void LaunchThreadIfIdleDoesNotLaunchSecondThreadIfFirstInProgress()
        {
            using (CommonRepoSetup setup = new CommonRepoSetup())
            {
                BlockedCreateDirectoryFileSystem fileSystem =
                    new BlockedCreateDirectoryFileSystem(setup.FileSystem.RootDirectory);
                using (ActionRunner prefetcher = new ActionRunner(
                                                        setup.Context.Tracer,
                                                        setup.Context.Enlistment,
                                                        setup.Context.FileSystem,
                                                        setup.GitObjects))
                {
                    prefetcher.LaunchThreadIfIdle().ShouldBeTrue();
                    prefetcher.LaunchThreadIfIdle().ShouldBeFalse();
                    fileSystem.UnblockCreateDirectory();
                    prefetcher.WaitForActionsToFinish();
                }
            }
        }

        private class BlockedCreateDirectoryFileSystem : MockFileSystem
        {
            private ManualResetEvent unblockCreateDirectory;

            public BlockedCreateDirectoryFileSystem(MockDirectory rootDirectory)
                : base(rootDirectory)
            {
                this.unblockCreateDirectory = new ManualResetEvent(initialState: false);
            }

            public void UnblockCreateDirectory()
            {
                this.unblockCreateDirectory.Set();
            }

            public override void CreateDirectory(string path)
            {
                this.unblockCreateDirectory.WaitOne();
                base.CreateDirectory(path);
            }
        }
    }
}
