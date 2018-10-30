#!/bin/bash

PKGNAME="VFSForGit"
PKGVERSION="0.0.0.1"
DMGNAME="$PKGNAME.$PKGVERSION"

if [ -d "/Volumes/${DMGNAME}" ]; then
	echo "Installing VFSForGit..."
	installCmd="sudo /usr/sbin/installer -pkg \"/Volumes/$DMGNAME/$PKGNAME.$PKGVERSION.pkg\" -target /"
	eval $installCmd	
else
	echo "Could not find GVFS Volume $DMGNAME"
	echo "Make sure that  $DMGNAME.dmg is mounted and try again."
	exit 1
fi