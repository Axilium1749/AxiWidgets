param([switch]$Run,[switch]$Test)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$env:DOTNET_CLI_HOME=Join-Path $PSScriptRoot '.tools/home'
$env:NUGET_PACKAGES=Join-Path $PSScriptRoot '.tools/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$sdk=Join-Path $PSScriptRoot '.tools/dotnet/dotnet.exe'
if (!(Test-Path $sdk)) { $sdk='dotnet' }
& $sdk restore AxiWidgets.slnx --configfile NuGet.Config -m:1
if ($LASTEXITCODE) {exit $LASTEXITCODE}
& $sdk build AxiWidgets.slnx -c Release --no-restore -m:1
if ($LASTEXITCODE) {exit $LASTEXITCODE}
if ($Test) { & $sdk run --project tests/AxiWidgets.Tests -c Release --no-build; if($LASTEXITCODE){exit $LASTEXITCODE} }
if ($Run) { & $sdk src/AxiWidgets/bin/Release/net10.0-windows/AxiWidgets.dll }
