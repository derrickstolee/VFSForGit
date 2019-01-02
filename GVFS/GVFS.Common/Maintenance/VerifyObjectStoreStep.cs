using GVFS.Common.Git;
using GVFS.Common.Tracing;
using System;
using System.IO;

namespace GVFS.Common.Maintenance
{
    public class VerifyObjectStoreStep : GitMaintenanceStep
    {
        public const string VerifyObjectsLastRunFilename = "verify-objects.time";
        private readonly bool forceRun;

        public VerifyObjectStoreStep(GVFSContext context, bool forceRun = false)
            : base(context, requireObjectCacheLock: !forceRun)
        {
            this.forceRun = forceRun;
        }

        public override string Area => nameof(VerifyObjectStoreStep);
        protected override string LastRunTimeFilePath => Path.Combine(this.Context.Enlistment.GitObjectsRoot, "info", VerifyObjectsLastRunFilename);
        protected override TimeSpan TimeBetweenRuns => TimeSpan.FromDays(1);

        protected override void PerformMaintenance()
        {
            using (ITracer activity = this.Context.Tracer.StartActivity(this.Area, EventLevel.Verbose))
            {
                if (!this.forceRun && !this.EnoughTimeBetweenRuns())
                {
                    activity.RelatedWarning($"Skipping {nameof(LooseObjectsStep)} due to not enough time between runs");
                    return;
                }

                GitProcess.Result graphResult = this.RunGitCommand((process) => process.VerifyCommitGraph(this.Context.Enlistment.GitObjectsRoot));

                if (graphResult.ExitCodeIsFailure)
                {
                    EventMetadata metadata = this.CreateEventMetadata();
                    metadata["CommitGraphVerifyOutput"] = graphResult.Output;
                    metadata["CommitGraphVerifyErrors"] = graphResult.Errors;

                    string commitGraphFile = Path.Combine(this.Context.Enlistment.GitObjectsRoot, "info", "commit-graph");
                    this.Context.FileSystem.TryDeleteFile(commitGraphFile);
                }

                GitProcess.Result midxResult = this.RunGitCommand((process) => process.VerifyMultiPackIndex(this.Context.Enlistment.GitObjectsRoot));

                if (midxResult.ExitCodeIsFailure)
                {
                    EventMetadata metadata = this.CreateEventMetadata();
                    metadata["MultiPackIndexVerifyOutput"] = midxResult.Output;
                    metadata["MultiPackIndexVerifyErrors"] = midxResult.Errors;

                    string midxFile = Path.Combine(this.Context.Enlistment.GitPackRoot, "multi-pack-index");
                    this.Context.FileSystem.TryDeleteFile(midxFile);

                    this.RunGitCommand((process) => process.WriteMultiPackIndex(this.Context.Enlistment.GitObjectsRoot));
                }
            }
        }
    }
}
