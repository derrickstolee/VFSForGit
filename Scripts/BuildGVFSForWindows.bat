@ECHO OFF
SETLOCAL
setlocal enabledelayedexpansion
CALL %~dp0\InitializeEnvironment.bat || EXIT /b 10

IF "%1"=="" (SET "Configuration=Debug") ELSE (SET "Configuration=%1")
IF "%2"=="" (SET "GVFSVersion=0.2.173.2") ELSE (SET "GVFSVersion=%2")

SET SolutionConfiguration=%Configuration%.Windows

SET nuget="%VFS_TOOLSDIR%\nuget.exe"
IF NOT EXIST %nuget% (
  mkdir %nuget%\..
  powershell -ExecutionPolicy Bypass -Command "Invoke-WebRequest 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe' -OutFile %nuget%"
)

msbuild.exe %VFS_SRCDIR%\GVFS.sln /p:GVFSVersion=%GVFSVersion% /p:Configuration=%SolutionConfiguration% /p:Platform=x64 || exit /b 1

dotnet publish %VFS_SRCDIR%\GVFS\FastFetch\FastFetch.csproj /p:Configuration=%Configuration% /p:Platform=x64 /p:SolutionDir=%VFS_SRCDIR%\ --runtime win-x64 --framework netcoreapp2.1 --self-contained --output %VFS_PUBLISHDIR%\FastFetch || exit /b 1
ENDLOCAL
