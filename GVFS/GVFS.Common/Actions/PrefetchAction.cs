using GVFS.Common.Git;
using GVFS.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace GVFS.Common.Actions
{
    public class PrefetchAction : IAction
    {
        private const int IoFailureRetryDelayMS = 50;
        private const int LockWaitTimeMs = 100;
        private const int WaitingOnLockLogThreshold = 50;
        private const string PrefetchCommitsAndTreesLock = "prefetch-commits-trees.lock";
        private readonly TimeSpan timeBetweenPrefetches = TimeSpan.FromMinutes(70);
        private ActionContext context;
        private bool stopping;

        public PrefetchAction(ActionContext context)
        {
            this.context = context;
        }

        public string TelemetryKey => nameof(PrefetchAction);

        public void Execute()
        {
            this.TryPrefetchCommitsAndTrees(out string error);
        }

        public bool TryPrefetchCommitsAndTrees(out string error)
        {
            using (FileBasedLock prefetchLock = GVFSPlatform.Instance.CreateFileBasedLock(
                    this.context.FileSystem,
                    this.context.Tracer,
                    Path.Combine(this.context.Enlistment.GitPackRoot, PrefetchCommitsAndTreesLock)))
            {
                if (!this.WaitUntilLockIsAcquired(prefetchLock))
                {
                    error = "Unable to acquire prefetch lock";
                    return false;
                }

                long maxGoodTimeStamp;

                // TODO: these actions are not interruptible! We need them to halt on a Stop().

                this.context.GitObjects.DeleteStaleTempPrefetchPackAndIdxs();

                if (!this.TryGetMaxGoodPrefetchTimestamp(out maxGoodTimeStamp, out error))
                {
                    return false;
                }

                if (!this.context.GitObjects.TryDownloadPrefetchPacks(maxGoodTimeStamp, out List<string> packIndexes))
                {
                    return false;
                }
            }

            return true;
        }

        public bool IsReady(DateTime now)
        {
            long last;
            string error;

            if (!this.context.GitObjects.IsUsingCacheServer())
            {
                // We don't prefetch in the background when not using a cache server, as that
                // can overload the central server. Instead, rely on the prefetch verb (run as
                // part of 'git fetch').
                return false;
            }

            // TODO: Even this timestamp check needs to be able to be halted.

            if (!this.TryGetMaxGoodPrefetchTimestamp(out last, out error))
            {
                this.context.Tracer.RelatedError(error);
                return false;
            }

            DateTime lastDateTime = EpochConverter.FromUnixEpochSeconds(last);

            return now <= lastDateTime + this.timeBetweenPrefetches;
        }

        public void Stop()
        {
            this.stopping = true;

            // TODO: How to halt the Git activity during a prefetch?
        }

        public bool TryGetMaxGoodPrefetchTimestamp(
            out long maxGoodTimestamp,
            out string error)
        {
            this.context.FileSystem.CreateDirectory(this.context.Enlistment.GitPackRoot);

            string[] packs = this.context.GitObjects.ReadPackFileNames(
                                this.context.Enlistment.GitPackRoot,
                                GVFSConstants.PrefetchPackPrefix);

            List<PrefetchPackInfo> orderedPacks = packs
                .Where(pack => GetTimestamp(pack).HasValue)
                .Select(pack => new PrefetchPackInfo(GetTimestamp(pack).Value, pack))
                .OrderBy(packInfo => packInfo.Timestamp)
                .ToList();

            maxGoodTimestamp = -1;

            int firstBadPack = -1;
            for (int i = 0; i < orderedPacks.Count; ++i)
            {
                long timestamp = orderedPacks[i].Timestamp;
                string packPath = orderedPacks[i].Path;
                string idxPath = Path.ChangeExtension(packPath, ".idx");
                if (!this.context.FileSystem.FileExists(idxPath))
                {
                    EventMetadata metadata = new EventMetadata();
                    metadata.Add("pack", packPath);
                    metadata.Add("idxPath", idxPath);
                    metadata.Add("timestamp", timestamp);
                    GitProcess.Result indexResult = this.context.GitObjects.IndexPackFile(packPath);
                    if (indexResult.HasErrors)
                    {
                        firstBadPack = i;

                        metadata.Add("Errors", indexResult.Errors);
                        this.context.Tracer.RelatedWarning(metadata, $"{nameof(TryGetMaxGoodPrefetchTimestamp)}: Found pack file that's missing idx file, and failed to regenerate idx");
                        break;
                    }
                    else
                    {
                        maxGoodTimestamp = timestamp;

                        metadata.Add(TracingConstants.MessageKey.InfoMessage, $"{nameof(TryGetMaxGoodPrefetchTimestamp)}: Found pack file that's missing idx file, and regenerated idx");
                        this.context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(TryGetMaxGoodPrefetchTimestamp)}_RebuildIdx", metadata);
                    }
                }
                else
                {
                    maxGoodTimestamp = timestamp;
                }
            }

            if (firstBadPack != -1)
            {
                const int MaxDeleteRetries = 200; // 200 * IoFailureRetryDelayMS (50ms) = 10 seconds
                const int RetryLoggingThreshold = 40; // 40 * IoFailureRetryDelayMS (50ms) = 2 seconds

                // Before we delete _any_ pack-files, we need to delete the multi-pack-index, which
                // may refer to those packs.

                EventMetadata metadata = new EventMetadata();
                string midxPath = Path.Combine(this.context.Enlistment.GitPackRoot, "multi-pack-index");
                metadata.Add("path", midxPath);
                metadata.Add(TracingConstants.MessageKey.InfoMessage, $"{nameof(TryGetMaxGoodPrefetchTimestamp)} deleting multi-pack-index");
                this.context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(TryGetMaxGoodPrefetchTimestamp)}_DeleteMultiPack_index", metadata);
                if (!this.context.FileSystem.TryWaitForDelete(this.context.Tracer, midxPath, IoFailureRetryDelayMS, MaxDeleteRetries, RetryLoggingThreshold))
                {
                    error = $"Unable to delete {midxPath}";
                    return false;
                }

                // Delete packs and indexes in reverse order so that if prefetch is killed, subseqeuent prefetch commands will
                // find the right starting spot.
                for (int i = orderedPacks.Count - 1; i >= firstBadPack; --i)
                {
                    string packPath = orderedPacks[i].Path;
                    string idxPath = Path.ChangeExtension(packPath, ".idx");

                    metadata = new EventMetadata();
                    metadata.Add("path", idxPath);
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, $"{nameof(TryGetMaxGoodPrefetchTimestamp)} deleting bad idx file");
                    this.context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(TryGetMaxGoodPrefetchTimestamp)}_DeleteBadIdx", metadata);
                    if (!this.context.FileSystem.TryWaitForDelete(this.context.Tracer, idxPath, IoFailureRetryDelayMS, MaxDeleteRetries, RetryLoggingThreshold))
                    {
                        error = $"Unable to delete {idxPath}";
                        return false;
                    }

                    metadata = new EventMetadata();
                    metadata.Add("path", packPath);
                    metadata.Add(TracingConstants.MessageKey.InfoMessage, $"{nameof(TryGetMaxGoodPrefetchTimestamp)} deleting bad pack file");
                    this.context.Tracer.RelatedEvent(EventLevel.Informational, $"{nameof(TryGetMaxGoodPrefetchTimestamp)}_DeleteBadPack", metadata);
                    if (!this.context.FileSystem.TryWaitForDelete(this.context.Tracer, packPath, IoFailureRetryDelayMS, MaxDeleteRetries, RetryLoggingThreshold))
                    {
                        error = $"Unable to delete {packPath}";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        private static long? GetTimestamp(string packName)
        {
            string filename = Path.GetFileName(packName);
            if (!filename.StartsWith(GVFSConstants.PrefetchPackPrefix))
            {
                return null;
            }

            string[] parts = filename.Split('-');
            long parsed;
            if (parts.Length > 1 && long.TryParse(parts[1], out parsed))
            {
                return parsed;
            }

            return null;
        }

        private bool WaitUntilLockIsAcquired(FileBasedLock fileBasedLock)
        {
            int attempt = 0;
            while (!this.stopping)
            {
                if (fileBasedLock.TryAcquireLock())
                {
                    return true;
                }

                Thread.Sleep(LockWaitTimeMs);
                ++attempt;
                if (attempt == WaitingOnLockLogThreshold)
                {
                    attempt = 0;
                    this.context.Tracer.RelatedInfo("WaitUntilLockIsAcquired: Waiting to acquire prefetch lock");
                }
            }

            return false;
        }

        private class PrefetchPackInfo
        {
            public PrefetchPackInfo(long timestamp, string path)
            {
                this.Timestamp = timestamp;
                this.Path = path;
            }

            public long Timestamp { get; }
            public string Path { get; }
        }
    }
}
