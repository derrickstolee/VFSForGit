#!/bin/bash

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

PACKAGEVERSION=$2
if [ -z $PACKAGEVERSION ]; then
  PACKAGEVERSION="1.0.0.1"
fi

SCRIPTDIR="$(dirname ${BASH_SOURCE[0]})"

# convert to an absolute path because it is required by `dotnet publish`
pushd $SCRIPTDIR
SCRIPTDIR="$(pwd)"
popd

SRCDIR=$SCRIPTDIR/../..
ROOTDIR=$SRCDIR/..
BUILDOUTPUT=$ROOTDIR/BuildOutput
PUBLISHDIR=$ROOTDIR/Publish
INSTALLROOT=$ROOTDIR/InstallRoot
VFSFORGITDESTINATION="usr/local/vfsforgit"
INSTALLERPACKAGENAME="VFSForGit.$PACKAGEVERSION"

echo "SRCDIR: ${SRCDIR}"
echo "ROOTDIR: ${ROOTDIR}"
echo "BUILDOUTPUT: ${BUILDOUTPUT}"
echo "PUBLISHDIR: ${PUBLISHDIR}"

function CheckBuildIsAvailable()
{
	if [ ! -d $BUILDOUTPUT ] || [ ! -d $PUBLISHDIR ]; then
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
	echo $mkdirVfsForGit
	eval $mkdirVfsForGit || exit 1
	
	mkdirBin="mkdir -p \"${INSTALLROOT}/usr/local/bin\""
	echo $mkdirBin
	eval $mkdirBin || exit 1
}

function CopyBinariesToInstall()
{
	copyPublishDirectory="cp -Rf \"${PUBLISHDIR}\"/* \"${INSTALLROOT}/${VFSFORGITDESTINATION}/.\""
	echo $copyPublishDirectory
	eval $copyPublishDirectory || exit 1
	
	removeTestAssemblies="find \"${INSTALLROOT}/${VFSFORGITDESTINATION}\" -name \"*GVFS.*Tests*\" -exec rm -f \"{}\" \";\""
	echo $removeTestAssemblies
	eval $removeTestAssemblies || exit 1
	
	removeDataDirectory="rm -Rf \"${INSTALLROOT}/${VFSFORGITDESTINATION}/Data\""
	echo $removeDataDirectory
	eval $removeDataDirectory || exit 1
	
	copyKext="cp -Rf \"${BUILDOUTPUT}/ProjFS.Mac/Native/Build/Products/$CONFIGURATION\"/* \"${INSTALLROOT}/${VFSFORGITDESTINATION}/.\""
	echo $copyKext
	eval $copyKext || exit 1
	
	currentDirectory=`pwd`
	cd "${INSTALLROOT}/usr/local/bin"
	linkCommand="ln -s ../vfsforgit/gvfs gvfs"
	echo $linkCommand
	eval $linkCommand
	cd $currentDirectory
}

function CreateInstaller()
{
	pkgBuildCommand="/usr/bin/pkgbuild --identifier com.microsoft.vfsforgit.pkg --root \"${INSTALLROOT}\" \"${ROOTDIR}\"/$INSTALLERPACKAGENAME.pkg"
	echo $pkgBuildCommand
	eval $pkgBuildCommand || exit 1
}

function CreateDiskImage
{
	createDmgRoot="mkdir -p \"${ROOTDIR}/$INSTALLERPACKAGENAME\""
	echo $createDmgRoot
	eval $createDmgRoot
	
	movePkgToDmgRoot="mv \"${ROOTDIR}\"/$INSTALLERPACKAGENAME.pkg \"${ROOTDIR}/$INSTALLERPACKAGENAME\"/."
	echo $movePkgToDmgRoot
	eval $movePkgToDmgRoot
	
	dmgBuildCommand="/usr/bin/hdiutil create \"${ROOTDIR}\"/$INSTALLERPACKAGENAME.dmg -ov -srcfolder \"${ROOTDIR}/$INSTALLERPACKAGENAME\""
	echo $dmgBuildCommand
	eval $dmgBuildCommand || exit 1
	
	deleteDmgRoot="rm -Rf \"${ROOTDIR}/$INSTALLERPACKAGENAME\""
	echo $deleteDmgRoot
	eval $deleteDmgRoot
}

CheckBuildIsAvailable

CreateInstallerRoot

CopyBinariesToInstall

SetPermissions

CreateInstaller

CreateDiskImage