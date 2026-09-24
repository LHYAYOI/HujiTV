$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$vsLocation = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsLocation) { throw 'Visual Studio C++ toolchain is required.' }
$pluginDir = Join-Path $projectRoot 'Assets/ReplayCapture/Plugins/Editor/x86_64'
$buildDir = Join-Path $projectRoot 'Artifacts/ReplayCapture/NativeBuild'
New-Item -ItemType Directory -Force -Path $pluginDir,$buildDir | Out-Null
$source = Join-Path $PSScriptRoot 'ReplayMediaFoundation.cpp.txt'
$dll = Join-Path $pluginDir 'ReplayMediaFoundation.dll'
$setup = Join-Path $vsLocation 'VC/Auxiliary/Build/vcvars64.bat'
# Fixed compiler invocation; no filesystem moves/deletes are delegated across shells.
$compile = "call `"$setup`" >nul && cl /TP /nologo /std:c++17 /EHsc /O2 /MD /LD `"$source`" /Fo`"$buildDir/ReplayMediaFoundation.obj`" /link /OUT:`"$dll`" /IMPLIB:`"$buildDir/ReplayMediaFoundation.lib`" mfplat.lib mfreadwrite.lib mfuuid.lib ole32.lib oleaut32.lib"
& $env:ComSpec /d /s /c $compile
if ($LASTEXITCODE -ne 0) { throw "Native build failed: $LASTEXITCODE" }

