﻿using GSD.Common.Tracing;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace GSD.Common.Maintenance
{
    public class GitMaintenanceQueue
    {
        private readonly object queueLock = new object();
        private GSDContext context;
        private BlockingCollection<GitMaintenanceStep> queue = new BlockingCollection<GitMaintenanceStep>();
        private GitMaintenanceStep currentStep;

        public GitMaintenanceQueue(GSDContext context)
        {
            this.context = context;
            Thread worker = new Thread(() => this.RunQueue());
            worker.Name = "MaintenanceWorker";
            worker.IsBackground = true;
            worker.Start();
        }

        public bool TryEnqueue(GitMaintenanceStep step)
        {
            try
            {
                lock (this.queueLock)
                {
                    if (this.queue == null)
                    {
                        return false;
                    }

                    this.queue.Add(step);
                    return true;
                }
            }
            catch (InvalidOperationException)
            {
                // We called queue.CompleteAdding()
            }

            return false;
        }

        public void Stop()
        {
            lock (this.queueLock)
            {
                this.queue?.CompleteAdding();
            }

            this.currentStep?.Stop();
        }

        /// <summary>
        /// This method is public for test purposes only.
        /// </summary>
        public bool EnlistmentRootReady()
        {
            // If a user locks their drive or disconnects an external drive while the mount process
            // is running, then it will appear as if the directories below do not exist or throw
            // a "Device is not ready" error.
            try
            {
                return this.context.FileSystem.DirectoryExists(this.context.Enlistment.EnlistmentRoot)
                         && this.context.FileSystem.DirectoryExists(this.context.Enlistment.GitObjectsRoot);
            }
            catch (IOException)
            {
                return false;
            }
        }

        private void RunQueue()
        {
            while (true)
            {
                // We cannot take the lock here, as TryTake is blocking.
                // However, this is the place to set 'this.queue' to null.
                if (!this.queue.TryTake(out this.currentStep, Timeout.Infinite)
                    || this.queue.IsAddingCompleted)
                {
                    lock (this.queueLock)
                    {
                        // A stop was requested
                        this.queue?.Dispose();
                        this.queue = null;
                        return;
                    }
                }

                if (this.EnlistmentRootReady())
                {
                    try
                    {
                        this.currentStep.Execute();
                    }
                    catch (Exception e)
                    {
                        this.LogErrorAndExit(
                            area: nameof(GitMaintenanceQueue),
                            methodName: nameof(this.RunQueue),
                            exception: e);
                    }
                }
            }
        }

        private void LogError(string area, string methodName, Exception exception)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Area", area);
            metadata.Add("Method", methodName);
            metadata.Add("ExceptionMessage", exception.Message);
            metadata.Add("StackTrace", exception.StackTrace);
            this.context.Tracer.RelatedError(
                metadata: metadata,
                message: area + ": Unexpected Exception while running maintenance steps (fatal): " + exception.Message,
                keywords: Keywords.Telemetry);
        }

        private void LogErrorAndExit(string area, string methodName, Exception exception)
        {
            this.LogError(area, methodName, exception);
            Environment.Exit((int)ReturnCode.GenericError);
        }
    }
}
