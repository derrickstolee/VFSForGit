#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

PACKAGEVERSION=$2
if [ -z $PACKAGEVERSION ]; then
  PACKAGEVERSION="1.0.0.1"
fi

ROOTDIR=$VFS_SRCDIR/..
PUBLISHDIR=$ROOTDIR/Publish
INSTALLROOT=$ROOTDIR/InstallRoot
VFSFORGITDESTINATION="usr/local/vfsforgit"
INSTALLERPACKAGENAME="VFSForGit.$PACKAGEVERSION"

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
	pkgBuildCommand="/usr/bin/pkgbuild --identifier com.microsoft.vfsforgit.pkg --root \"${INSTALLROOT}\" \"${ROOTDIR}\"/$INSTALLERPACKAGENAME.pkg"
	eval $pkgBuildCommand || exit 1
}

function CreateDiskImage
{
	createDmgRoot="mkdir -p \"${ROOTDIR}/$INSTALLERPACKAGENAME\""
	eval $createDmgRoot
	
	movePkgToDmgRoot="mv \"${ROOTDIR}\"/$INSTALLERPACKAGENAME.pkg \"${ROOTDIR}/$INSTALLERPACKAGENAME\"/."
	eval $movePkgToDmgRoot
	
	dmgBuildCommand="/usr/bin/hdiutil create \"${VFS_OUTPUTDIR}\"/$INSTALLERPACKAGENAME.dmg -ov -srcfolder \"${ROOTDIR}/$INSTALLERPACKAGENAME\""
	eval $dmgBuildCommand || exit 1
	
	deleteDmgRoot="rm -Rf \"${ROOTDIR}/$INSTALLERPACKAGENAME\""
	eval $deleteDmgRoot	
}

CheckBuildIsAvailable

CreateInstallerRoot

CopyBinariesToInstall

SetPermissions

CreateInstaller

CreateDiskImage