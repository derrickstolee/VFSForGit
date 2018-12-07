﻿using GVFS.Common.Git;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GVFS.Common.Maintenance
{
    public class GitMaintenanceScheduler : IDisposable
    {
        private List<Timer> stepTimers;
        private GVFSContext context;
        private GitObjects gitObjects;
        private GitMaintenanceQueue queue;

        public GitMaintenanceScheduler(GVFSContext context, GitObjects gitObjects)
        {
            this.context = context;
            this.gitObjects = gitObjects;
            this.stepTimers = new List<Timer>();
            this.queue = new GitMaintenanceQueue(context);

            this.ScheduleRecurringSteps();
        }

        public void EnqueueOneTimeStep(GitMaintenanceStep step)
        {
            this.queue.TryEnqueue(step);
        }

        public void Dispose()
        {
            this.queue.Stop();

            foreach (Timer timer in this.stepTimers)
            {
                timer?.Dispose();
            }

            this.stepTimers = null;
        }

        private void ScheduleRecurringSteps()
        {
            if (this.context.Unattended)
            {
                return;
            }

            if (this.gitObjects.IsUsingCacheServer())
            {
                TimeSpan prefetchPeriod = TimeSpan.FromMinutes(15);
                this.stepTimers.Add(new Timer(
                    (state) => this.queue.TryEnqueue(new PrefetchStep(this.context, this.gitObjects, requireCacheLock: true)),
                    state: null,
                    dueTime: prefetchPeriod,
                    period: prefetchPeriod));
            }

            TimeSpan looseObjectsPeriod = TimeSpan.FromHours(12);
            this.stepTimers.Add(new Timer(
                (state) => this.queue.TryEnqueue(new LooseObjectsStep(this.context, requireCacheLock: true)),
                state: null,
                dueTime: looseObjectsPeriod,
                period: looseObjectsPeriod));

            TimeSpan packPeriod = TimeSpan.FromMinutes(1);
            this.stepTimers.Add(new Timer(
                (state) => this.queue.TryEnqueue(new GitPackMaintenanceStep(this.context, this.gitObjects)),
                state: null,
                dueTime: TimeSpan.FromSeconds(10),
                period: packPeriod));
        }
    }
}
