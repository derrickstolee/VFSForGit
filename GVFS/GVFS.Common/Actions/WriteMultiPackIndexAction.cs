using System;
using System.IO;

namespace GVFS.Common.Actions
{
    public class WriteMultiPackIndexAction : AbstractObjectCacheAction
    {
        public const string MultiPackIndex = "multi-pack-index";
        public const string MultiPackIndexLock = "multi-pack-index.lock";
        public const string InfoFile = "write-multi-pack-index-action.json";

        public WriteMultiPackIndexAction(ActionContext context) : base(context)
        {
        }

        public override string TelemetryKey => "WriteMultiPackIndexAction";

        /// <summary>
        /// We are ready if there is a pack-file with modified time newer
        /// than the multi-pack-index.
        /// </summary>
        public override bool IsReady(DateTime now)
        {
            string midxPath = Path.Combine(this.Context.Enlistment.GitPackRoot, MultiPackIndex);

            if (!this.Context.FileSystem.FileExists(midxPath))
            {
                return true;
            }

            DateTime multiPackIndexTime = this.Context.FileSystem
                                                     .GetFileProperties(midxPath)
                                                     .LastWriteTimeUTC;

            return this.HasNewerPrefetchPack(multiPackIndexTime);
        }

        protected override void RunGitAction()
        {
            string midxLock = Path.Combine(this.Context.Enlistment.GitPackRoot, MultiPackIndexLock);
            this.Context.FileSystem.TryDeleteFile(midxLock);

            this.RunGitCommand(process => process.WriteMultiPackIndex(this.Context.Enlistment.GitObjectsRoot));
        }
    }
}
