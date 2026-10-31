param()
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tools=Join-Path $repo '.tools'
$lock=Get-Content (Join-Path $repo 'scripts/toolchain.lock.json') -Raw | ConvertFrom-Json
$vc=Join-Path $tools $lock.native.msvc
$sdk=Join-Path $tools $lock.native.sdkInclude
$libs=Join-Path $tools $lock.native.sdkLib
$compiler=Join-Path $vc 'bin/Hostx64/x64/cl.exe'
if(!(Test-Path -LiteralPath $compiler) -or !(Test-Path -LiteralPath "$sdk/cppwinrt/winrt/base.h")){
    throw 'Native toolchain missing. Run ./scripts/Bootstrap.ps1 from the repository root first.'
}
$previousInclude=$env:INCLUDE;$previousLib=$env:LIB
$env:INCLUDE="$vc/include;$sdk/ucrt;$sdk/shared;$sdk/um;$sdk/winrt;$sdk/cppwinrt"
$env:LIB="$vc/lib/x64;$libs/um/x64;$libs/ucrt/x64"
$out=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
Push-Location $out
try {
    # /MT embeds the CRT. The pinned minimal toolset omits unused OLDNAMES aliases.
    & $compiler /nologo /utf-8 /std:c++20 /EHsc /W4 /WX /O2 /MT /LD /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX "$PSScriptRoot/CardRenderer.cpp" "$PSScriptRoot/SystemMetrics.cpp" /Fe:AxiWidgets.Native.dll /link /NODEFAULTLIB:OLDNAMES ole32.lib user32.lib dwmapi.lib windowsapp.lib CoreMessaging.lib d2d1.lib dxguid.lib d3d11.lib dwrite.lib pdh.lib powrprof.lib iphlpapi.lib dxgi.lib
    if($LASTEXITCODE){throw "Native compiler failed: $LASTEXITCODE"}
} finally {Pop-Location;$env:INCLUDE=$previousInclude;$env:LIB=$previousLib}
