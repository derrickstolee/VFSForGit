using GVFS.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GVFS.Common.Maintenance
{
    public class LibGit2PoolPerformanceTestStep : GitMaintenanceStep
    {
        public LibGit2PoolPerformanceTestStep(GVFSContext context)
            : base(context, requireObjectCacheLock: false)
        {
        }

        public override string Area => nameof(LibGit2PoolPerformanceTestStep);

        protected override void PerformMaintenance()
        {
            int numThreads = 100;
            int objectQueries = 100;

            using (ITracer activity = this.Context.Tracer.StartActivity(this.Area, EventLevel.LogAlways))
            {
                Random seeds = new Random();

                List<Thread> threads = new List<Thread>();
                for (int i = 0; i < numThreads; i++)
                {
                    threads.Add(new Thread(() =>
                    {
                        Random r = new Random(seeds.Next());

                        byte[] buf = new byte[20];

                        for (int j = 0; j < objectQueries; j++)
                        {
                            if (i == 0 && (j + 1) % 10 == 0)
                            {
                                activity.RelatedInfo("{0} active repos", this.Context.Repository.NumActiveLibGit2Repos);
                            }

                            r.NextBytes(buf);

                            try
                            {
                                string sha = SHA1Util.HexStringFromBytes(buf);
                                this.Context.Repository.ObjectExists(sha);
                            }
                            catch (Exception e)
                            {
                                activity.RelatedWarning(this.CreateEventMetadata(e), $"Exception in thread {i}");
                            }
                        }
                    }));
                }

                for (int i = 0; i < numThreads; i++)
                {
                    threads[i].Start();
                }

                for (int i = 0; i < numThreads; i++)
                {
                    threads[i].Join();
                }
            }
        }
    }
}
