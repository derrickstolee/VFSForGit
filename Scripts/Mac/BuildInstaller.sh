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
    echo $chmodCommand
    eval $chmodCommand || exit 1
}
 
function CreateInstallerRoot()
{
    mkdirVfsForGit="mkdir -p \"${INSTALLROOT}/$VFSFORGITDESTINATION\""
    eval $mkdirVfsForGit || exit 1
    
    mkdirBin="mkdir -p \"${INSTALLROOT}/usr/local/bin\""
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
    
    copyKext="cp -Rf \"${VFS_OUTPUTDIR}/ProjFS.Mac/Native/Build/Products/$CONFIGURATION\"/* \"${INSTALLROOT}/${VFSFORGITDESTINATION}/.\""
    eval $copyKext || exit 1
    
    currentDirectory=`pwd`
    cd "${INSTALLROOT}/usr/local/bin"
    linkCommand="ln -s ../vfsforgit/gvfs gvfs"
    eval $linkCommand
    cd $currentDirectory
}

function CreateInstaller()
{
    pkgRootDirectory="${VFS_OUTPUTDIR}/$INSTALLERPACKAGENAME"
    createPkgRootCommand="mkdir -p \"${pkgRootDirectory}\""
    eval $createPkgRootCommand || exit 1
    
    pkgBuildCommand="/usr/bin/pkgbuild --identifier $INSTALLERPACKAGEID --root \"${INSTALLROOT}\" \"${pkgRootDirectory}\"/$INSTALLERPACKAGENAME.pkg"
    eval $pkgBuildCommand || exit 1
    
    copyCommandLineInstaller="/bin/cp ${VFS_SCRIPTDIR}/Install_GVFS.command \"${pkgRootDirectory}\"/."
    eval $copyCommandLineInstaller || exit 1
}

function CreateDiskImage
{    
    dmgBuildCommand="/usr/bin/hdiutil create \"${VFS_OUTPUTDIR}\"/$INSTALLERPACKAGENAME.dmg -ov -srcfolder \"${VFS_OUTPUTDIR}/$INSTALLERPACKAGENAME\""
    eval $dmgBuildCommand || exit 1
    
    deleteDmgRoot="rm -Rf \"${VFS_OUTPUTDIR}/$INSTALLERPACKAGENAME\""
    eval $deleteDmgRoot 
}

CheckBuildIsAvailable

CreateInstallerRoot

CopyBinariesToInstall

SetPermissions

CreateInstaller

CreateDiskImage

