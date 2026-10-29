param([switch]$Run,[switch]$Test)
$ErrorActionPreference='Stop'
if($env:OS -ne 'Windows_NT' -or ![Environment]::Is64BitProcess){throw 'Use 64-bit PowerShell on Windows.'}
$sdk=Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if(!(Test-Path -LiteralPath $sdk)){throw 'Toolchain missing. Run ./scripts/Bootstrap.ps1 first.'}
$lock=Get-Content (Join-Path $PSScriptRoot 'scripts/toolchain.lock.json') -Raw | ConvertFrom-Json
Push-Location $PSScriptRoot
try {
    $env:DOTNET_ROOT=Split-Path $sdk -Parent
    $env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.tools/home'
    $env:NUGET_PACKAGES=Join-Path $PSScriptRoot '.tools/nuget'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
    $env:DOTNET_NOLOGO='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true'
    if((& $sdk --version | Out-String).Trim() -ne $lock.dotnet.version){throw 'SDK mismatch. Run ./scripts/Bootstrap.ps1.'}
    & $sdk restore AxiWidgets.slnx --configfile NuGet.Config -m:1
    if($LASTEXITCODE){throw "Restore failed: $LASTEXITCODE"}
    & $sdk build AxiWidgets.slnx -c Release --no-restore -m:1
    if($LASTEXITCODE){throw "Build failed: $LASTEXITCODE"}
    if($Test){
        & $sdk run --project tests/AxiWidgets.Tests -c Release --no-build
        if($LASTEXITCODE){throw "Tests failed: $LASTEXITCODE"}
    }
    if($Run){& $sdk src/AxiWidgets/bin/Release/net10.0-windows/AxiWidgets.dll}
} finally {Pop-Location}
