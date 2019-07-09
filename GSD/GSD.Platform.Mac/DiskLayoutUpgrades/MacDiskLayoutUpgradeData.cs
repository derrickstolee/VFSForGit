﻿using GSD.Common;
using GSD.DiskLayoutUpgrades;

namespace GSD.Platform.Mac
{
    public class MacDiskLayoutUpgradeData : IDiskLayoutUpgradeData
    {
        public DiskLayoutUpgrade[] Upgrades
        {
            get
            {
                return new DiskLayoutUpgrade[]
                {
                };
            }
        }

        public DiskLayoutVersion Version => new DiskLayoutVersion(
            currentMajorVersion: 0,
            currentMinorVersion: 0,
            minimumSupportedMajorVersion: 0);

        public bool TryParseLegacyDiskLayoutVersion(string dotGSDPath, out int majorVersion)
        {
            majorVersion = 0;
            return false;
        }
    }
}
