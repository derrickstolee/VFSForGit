using GVFS.Common.FileSystem;
using GVFS.Common.Git;
using GVFS.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GVFS.Common.Actions
{
    public class ActionRunner : IDisposable
    {
        private readonly TimeSpan timerPeriod = TimeSpan.FromMinutes(15);
        private readonly object launchThreadLock = new object();
        private readonly object swapActionLock = new object();
        private ActionContext context;
        private bool stopping;
        private List<IAction> actions;
        private Timer timer;
        private Thread thread;
        private IAction curAction;

        public ActionRunner(
            ITracer tracer,
            GVFSEnlistment enlistment,
            PhysicalFileSystem fileSystem,
            GitObjects gitObjects)
        {
            this.context = new ActionContext(tracer, enlistment, fileSystem, gitObjects);

            this.actions = new List<IAction>();
            this.actions.Add(new PrefetchAction(this.context));
            this.actions.Add(new WriteMultiPackIndexAction(this.context));
            this.actions.Add(new WriteCommitGraphAction(this.context));

            this.timer = new Timer(
                                (state) => this.LaunchThreadIfIdle(),
                                state: null,
                                dueTime: this.timerPeriod,
                                period: this.timerPeriod);
        }

        public void Dispose()
        {
            this.stopping = true;
            this.timer?.Dispose();
            this.timer = null;

            lock (this.swapActionLock)
            {
                this.curAction?.Stop();
            }
        }

        public bool LaunchThreadIfIdle()
        {
            try
            {
                lock (this.launchThreadLock)
                {
                    if (this.thread?.IsAlive == true)
                    {
                        this.context.Tracer.RelatedInfo(nameof(ActionRunner) + ": background thread not idle, skipping timed start");
                    }
                    else
                    {
                        this.thread = new Thread(() => this.RunActions());
                        this.thread.IsBackground = true;
                        this.thread.Start();
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception e)
            {
                this.LogUnhandledExceptionAndExit(
                    telemetryKey: nameof(ActionRunner),
                    methodName: nameof(this.LaunchThreadIfIdle),
                    exception: e);
                return false;
            }
        }

        /// <summary>
        /// This method is used for test purposes only.
        /// </summary>
        public void WaitForActionsToFinish()
        {
            this.thread?.Join();
        }

        private void RunActions()
        {
            foreach (IAction action in this.actions)
            {
                if (this.stopping)
                {
                    return;
                }

                lock (this.swapActionLock)
                {
                    this.curAction = action;
                }

                if (this.curAction.IsReady(DateTime.UtcNow))
                {
                    if (this.stopping)
                    {
                        return;
                    }

                    this.curAction.Execute();

                    lock (this.swapActionLock)
                    {
                        this.curAction = null;
                    }
                }
            }
        }

        private void LogUnhandledExceptionAndExit(string telemetryKey, string methodName, Exception exception)
        {
            EventMetadata metadata = new EventMetadata();
            metadata.Add("Method", methodName);
            metadata.Add("ExceptionMessage", exception.Message);
            metadata.Add("StackTrace", exception.StackTrace);
            this.context.Tracer.RelatedError(
                metadata: metadata,
                message: telemetryKey + ": Unexpected Exception while running prefetch background thread (fatal): " + exception.Message,
                keywords: Keywords.Telemetry);
            Environment.Exit((int)ReturnCode.GenericError);
        }
    }
}
