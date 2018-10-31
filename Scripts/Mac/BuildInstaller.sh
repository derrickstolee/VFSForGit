#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

PACKAGEVERSION=$2
if [ -z $PACKAGEVERSION ]; then
  PACKAGEVERSION="0.0.0.1"
fi

INSTALLROOT=$VFS_OUTPUTDIR/"InstallerRoot"
VFSFORGITDESTINATION="usr/local/vfsforgit"
KEXTDESTINATION="$VFSFORGITDESTINATION"
INSTALLERPACKAGENAME="VFSForGit.$PACKAGEVERSION"
INSTALLERPACKAGEID="com.vfsforgit.pkg"

function CheckBuildIsAvailable()
{
    if [ ! -d $VFS_OUTPUTDIR ] || [ ! -d $VFS_PUBLISHDIR ]; then
        echo "Could not find VFSForGit Build to package."
        exit 1
    fi
}

function SetPermissions()
{
    chmodCommand="chmod -R 755 \"${INSTALLROOT}\""
    eval $chmodCommand || exit 1
}
 
function CreateInstallerRoot()
{
    mkdirVfsForGit="mkdir -p \"${INSTALLROOT}/$VFSFORGITDESTINATION\""
    eval $mkdirVfsForGit || exit 1

    mkdirBin="mkdir -p \"${INSTALLROOT}/usr/local/bin\""
    eval $mkdirBin || exit 1

    mkdirBin="mkdir -p \"${INSTALLROOT}/$KEXTDESTINATION\""
    eval $mkdirBin || exit 1
}

function CopyBinariesToInstall()
{
    copyPublishDirectory="cp -Rf \"${VFS_PUBLISHDIR}\"/* \"${INSTALLROOT}/${VFSFORGITDESTINATION}/.\""
    eval $copyPublishDirectory || exit 1
    
    removeTestAssemblies="find \"${INSTALLROOT}/${VFSFORGITDESTINATION}\" -name \"*GVFS.*Tests*\" -exec rm -f \"{}\" \";\""
    eval $removeTestAssemblies || exit 1
    
    removeDataDirectory="rm -Rf \"${INSTALLROOT}/${VFSFORGITDESTINATION}/Data\""
    eval $removeDataDirectory || exit 1
    
    copyNative="cp -Rf \"${VFS_OUTPUTDIR}/ProjFS.Mac/Native/Build/Products/$CONFIGURATION\"/*.dylib \"${INSTALLROOT}/${VFSFORGITDESTINATION}/.\""
    eval $copyNative || exit 1
    
    copyNative="cp -Rf \"${VFS_OUTPUTDIR}/ProjFS.Mac/Native/Build/Products/$CONFIGURATION\"/prjfs-log \"${INSTALLROOT}/${VFSFORGITDESTINATION}/.\""
    eval $copyNative || exit 1
    
    copyKext="cp -Rf \"${VFS_OUTPUTDIR}/ProjFS.Mac/Native/Build/Products/$CONFIGURATION\"/PrjFSKext.kext \"${INSTALLROOT}/${KEXTDESTINATION}/.\""
    eval $copyKext || exit 1
    
    currentDirectory=`pwd`
    cd "${INSTALLROOT}/usr/local/bin"
    linkCommand="ln -s ../vfsforgit/gvfs gvfs"
    eval $linkCommand
    cd $currentDirectory
}

function CreateInstaller()
{
    pkgBuildCommand="/usr/bin/pkgbuild --identifier $INSTALLERPACKAGEID --root \"${INSTALLROOT}\" \"${VFS_OUTPUTDIR}\"/$INSTALLERPACKAGENAME.pkg"
    eval $pkgBuildCommand || exit 1
}

CheckBuildIsAvailable

CreateInstallerRoot

CopyBinariesToInstall

SetPermissions

CreateInstaller

