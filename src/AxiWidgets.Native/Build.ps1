param()
$ErrorActionPreference='Stop'
$tools=Join-Path $PSScriptRoot '../../.tools/native-toolchain'
$vc=Join-Path $tools 'msvc/Contents/VC/Tools/MSVC/14.44.35207'
$sdk=Join-Path $tools 'microsoft.windows.sdk.cpp/c/Include/10.0.26100.0'
$libs=Join-Path $tools 'microsoft.windows.sdk.cpp.x64/c'
if(!(Test-Path "$vc/bin/Hostx64/x64/cl.exe")){throw 'Local native C++ toolchain missing under .tools/native-toolchain'}
$env:INCLUDE="$vc/include;$sdk/ucrt;$sdk/shared;$sdk/um;$sdk/winrt;$sdk/cppwinrt"
$env:LIB="$vc/lib/x64;$libs/um/x64;$libs/ucrt/x64"
$out=Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $out | Out-Null
Push-Location $out
try {
    & "$vc/bin/Hostx64/x64/cl.exe" /nologo /utf-8 /std:c++20 /EHsc /W4 /O2 /MT /LD /DUNICODE /D_UNICODE /DWIN32_LEAN_AND_MEAN /DNOMINMAX "$PSScriptRoot/CardRenderer.cpp" /Fe:AxiWidgets.Native.dll /link /NODEFAULTLIB:OLDNAMES user32.lib dwmapi.lib windowsapp.lib CoreMessaging.lib d2d1.lib dxguid.lib d3d11.lib dwrite.lib
    if($LASTEXITCODE){exit $LASTEXITCODE}
} finally {Pop-Location}
