using GVFS.Common.Git;
using GVFS.Common.Tracing;
using System.Diagnostics;

namespace GVFS.Common.Maintenance
{
    public class LibGit2RepoPerfStep : GitMaintenanceStep
    {
        public LibGit2RepoPerfStep(GVFSContext context)
            : base(context, requireObjectCacheLock: false)
        {
        }

        public override string Area => nameof(LibGit2RepoPerfStep);

        protected override void PerformMaintenance()
        {
            const int N = 1000;

            using (ITracer activity = this.Context.Tracer.StartActivity(this.Area + "_SameRepo", EventLevel.Informational))
            {
                Stopwatch sw = Stopwatch.StartNew();
                using (LibGit2Repo repo = new LibGit2Repo(activity, this.Context.Enlistment.WorkingDirectoryRoot))
                {
                    for (int i = 0; i < N; i++)
                    {
                        repo.IsBlob("9d4f5e6f3b7079cd7f61aa9f639a9195a4e07336");
                    }
                }

                activity.RelatedInfo("Time for {0} IsBlob calls: {1} ms", N, sw.ElapsedMilliseconds);
            }

            using (ITracer activity = this.Context.Tracer.StartActivity(this.Area + "_NewRepos", EventLevel.Informational))
            {
                Stopwatch sw = Stopwatch.StartNew();
                for (int i = 0; i < N; i++)
                {
                    using (LibGit2Repo repo = new LibGit2Repo(activity, this.Context.Enlistment.WorkingDirectoryRoot))
                    {
                        repo.IsBlob("9d4f5e6f3b7079cd7f61aa9f639a9195a4e07336");
                    }
                }

                activity.RelatedInfo("Time for {0} IsBlob calls: {1} ms", N, sw.ElapsedMilliseconds);
            }
        }
    }
}
