﻿using GVFS.Common.Tracing;

namespace GVFS.Common.FileSystem
{
    public interface IPlatformFileSystem
    {
        bool SupportsFileMode { get; }
        void FlushFileBuffers(string path);
        void MoveAndOverwriteFile(string sourceFileName, string destinationFilename);
        void CreateHardLink(string newLinkFileName, string existingFileName);
        bool TryGetNormalizedPath(string path, out string normalizedPath, out string errorMessage);
        void ChangeMode(string path, ushort mode);
        bool IsExecutable(string filePath);
        bool IsSocket(string filePath);
        bool TryCreateDirectoryWithAdminAndUserModifyPermissions(string directoryPath, out string error);
        bool TryCreateOrUpdateDirectoryToAdminModifyPermissions(ITracer tracer, string directoryPath, out string error);
    }
}
