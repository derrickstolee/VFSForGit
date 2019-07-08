﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GSD.Common.Maintenance
{
    public class GitProcessChecker
    {
        public virtual IEnumerable<int> GetRunningGitProcessIds()
        {
            Process[] allProcesses = Process.GetProcesses();
            return allProcesses
                .Where(x => x.ProcessName.Equals("git", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Id);
        }
    }
}
