using GVFS.Common.FileSystem;
using GVFS.Common.Git;
using GVFS.Common.Tracing;

namespace GVFS.Common.Actions
{
    public class ActionContext
    {
        public ActionContext(
            ITracer tracer,
            GVFSEnlistment enlistment,
            PhysicalFileSystem fileSystem,
            GitObjects gitObjects)
        {
            this.Tracer = tracer;
            this.Enlistment = enlistment;
            this.FileSystem = fileSystem;
            this.GitObjects = gitObjects;
        }

        public ITracer Tracer { get; }
        public GVFSEnlistment Enlistment { get; }
        public PhysicalFileSystem FileSystem { get; }
        public GitObjects GitObjects { get; }
    }
}
