@ECHO OFF
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

taskkill /F /T /FI "IMAGENAME eq git.exe"
taskkill /F /T /FI "IMAGENAME eq GSD.exe"
taskkill /F /T /FI "IMAGENAME eq GSD.Mount.exe"

if not exist "c:\Program Files\GSD" goto :end

call %VFS_SCRIPTSDIR%\StopAllServices.bat

REM The GSD uninstaller will not remove prjflt, and so we must remove it ourselves first.  If we don't, the non-inbox ProjFS
REM will cause problems next time the inbox ProjFS is enabled
call %VFS_SCRIPTSDIR%\StopService.bat prjflt
rundll32.exe SETUPAPI.DLL,InstallHinfSection DefaultUninstall 128 C:\Program Files\GSD\Filter\prjflt.inf

REM Find the latest uninstaller file by date and run it. Goto the next step after a single execution.
for /F "delims=" %%f in ('dir "c:\Program Files\GSD\unins*.exe" /B /S /O:-D') do %%f /VERYSILENT /SUPPRESSMSGBOXES /NORESTART & goto :deleteGSD

:deleteGSD
rmdir /q/s "c:\Program Files\GSD"

REM Delete ProgramData\GSD directory (logs, downloaded upgrades, repo-registry, gvfs.config). It can affect the behavior of a future GSD install.
if exist "C:\ProgramData\GSD" rmdir /q/s "C:\ProgramData\GSD"

:end
