using GVFS.Common.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;

namespace GVFS.Common.Actions
{
    public class WriteCommitGraphAction : AbstractObjectCacheAction
    {
        public const int MaxNumPacks = 10;

        public WriteCommitGraphAction(ActionContext context) : base(context)
        {
        }

        public override string TelemetryKey => "WriteCommitGraphAction";

        public override bool IsReady(DateTime now)
        {
            DateTime modifiedTime = this.GetCommitGraphModifiedTime();

            return this.HasNewerPrefetchPack(modifiedTime);
        }

        protected override void RunGitAction()
        {
            DateTime modifiedTime = this.GetCommitGraphModifiedTime();

            List<string> packs = new List<string>();

            foreach (DirectoryItemInfo info in this.Context.FileSystem.ItemsInDirectory(this.Context.Enlistment.GitPackRoot))
            {
                if (info.Name.StartsWith("prefetch-") && info.Name.EndsWith(".idx"))
                {
                    DateTime idxTime = this.Context.FileSystem
                                                    .GetFileProperties(info.FullName)
                                                    .LastWriteTimeUTC;
                    if (idxTime.CompareTo(modifiedTime) > 0)
                    {
                        packs.Add(info.Name);
                    }
                }
            }

            if (packs.Count > MaxNumPacks)
            {
                packs.Sort();
                packs.RemoveRange(0, packs.Count - MaxNumPacks);
            }

            this.RunGitCommand(process => process.WriteCommitGraph(this.Context.Enlistment.GitObjectsRoot, packs));
        }

        private DateTime GetCommitGraphModifiedTime()
        {
            string commitGraphPath = Path.Combine(this.Context.Enlistment.GitObjectsRoot, "info", "commit-graph");

            if (!this.Context.FileSystem.FileExists(commitGraphPath))
            {
                return DateTime.MinValue;
            }

            return this.Context.FileSystem.GetFileProperties(commitGraphPath)
                                          .LastWriteTimeUTC;
        }
    }
}
