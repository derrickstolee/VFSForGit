using System;

namespace GVFS.Common.Actions
{
    public interface IAction
    {
        string TelemetryKey { get; }
        bool IsReady(DateTime now);
        void Execute();
        void Stop();
    }
}
