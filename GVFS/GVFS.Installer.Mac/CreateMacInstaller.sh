#!/bin/bash

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

PACKAGEVERSION=$2
if [ -z $PACKAGEVERSION ]; then
  PACKAGEVERSION="0.0.0.1"
fi

BUILDOUTPUTDIR=$3
if [ -z $BUILDOUTPUTDIR ]; then
  echo "Build output directory not specified"
  exit 1
fi

STAGINGDIR=$BUILDOUTPUTDIR"Staging"
VFSFORGITDESTINATION="usr/local/vfsforgit"
KEXTDESTINATION="/Library/Extensions"
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
    chmodCommand="chmod -R 755 \"${STAGINGDIR}\""
    eval $chmodCommand || exit 1
}
 
function CreateInstallerRoot()
{
    mkdirVfsForGit="mkdir -p \"${STAGINGDIR}/$VFSFORGITDESTINATION\""
    eval $mkdirVfsForGit || exit 1

    mkdirBin="mkdir -p \"${STAGINGDIR}/usr/local/bin\""
    eval $mkdirBin || exit 1

    mkdirBin="mkdir -p \"${STAGINGDIR}/$KEXTDESTINATION\""
    eval $mkdirBin || exit 1
}

function CopyBinariesToInstall()
{
    copyPublishDirectory="cp -Rf \"${VFS_PUBLISHDIR}\"/* \"${STAGINGDIR}/${VFSFORGITDESTINATION}/.\""
    eval $copyPublishDirectory || exit 1
    
    removeTestAssemblies="find \"${STAGINGDIR}/${VFSFORGITDESTINATION}\" -name \"*GVFS.*Tests*\" -exec rm -f \"{}\" \";\""
    eval $removeTestAssemblies || exit 1
    
    removeDataDirectory="rm -Rf \"${STAGINGDIR}/${VFSFORGITDESTINATION}/Data\""
    eval $removeDataDirectory || exit 1
    
    copyNative="cp -Rf \"${VFS_OUTPUTDIR}/ProjFS.Mac/Native/Build/Products/$CONFIGURATION\"/*.dylib \"${STAGINGDIR}/${VFSFORGITDESTINATION}/.\""
    eval $copyNative || exit 1
    
    copyNative="cp -Rf \"${VFS_OUTPUTDIR}/ProjFS.Mac/Native/Build/Products/$CONFIGURATION\"/prjfs-log \"${STAGINGDIR}/${VFSFORGITDESTINATION}/.\""
    eval $copyNative || exit 1
    
    copyKext="cp -Rf \"${VFS_OUTPUTDIR}/ProjFS.Mac/Native/Build/Products/$CONFIGURATION\"/PrjFSKext.kext \"${STAGINGDIR}/${KEXTDESTINATION}/.\""
    eval $copyKext || exit 1
    
    currentDirectory=`pwd`
    cd "${STAGINGDIR}/usr/local/bin"
    linkCommand="ln -sf ../vfsforgit/gvfs gvfs"
    eval $linkCommand
    cd $currentDirectory
}

function CreateInstaller()
{
    pkgBuildCommand="/usr/bin/pkgbuild --identifier $INSTALLERPACKAGEID --root \"${STAGINGDIR}\" \"${BUILDOUTPUTDIR}\"$INSTALLERPACKAGENAME.pkg"
    eval $pkgBuildCommand || exit 1
}

function DeleteStaging()
{
    rmCommand="/bin/rm -Rf \"${STAGINGDIR}\""
    eval $rmCommand || exit 1
}

function Run()
{
    CheckBuildIsAvailable
    CreateInstallerRoot
    CopyBinariesToInstall
    SetPermissions
    CreateInstaller
    DeleteStaging
}

Run
