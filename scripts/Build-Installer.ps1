param([switch]$TestInstall)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
Push-Location $repo
try {
    & ./build.ps1 -Test
    $sdk=Join-Path $repo '.tools/dotnet/dotnet.exe'
    $env:DOTNET_ROOT=Split-Path $sdk -Parent
    $env:DOTNET_CLI_HOME=Join-Path $repo '.tools/home'
    $env:NUGET_PACKAGES=Join-Path $repo '.tools/nuget'
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
    $env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
    $env:DOTNET_NOLOGO='1'
    $env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true'
    [xml]$project=Get-Content src/AxiWidgets/AxiWidgets.csproj
    $version=[string]$project.Project.PropertyGroup.Version
    $publish=Join-Path $repo 'artifacts/publish'
    & $sdk publish src/AxiWidgets/AxiWidgets.csproj -c Release -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishSingleFile=false -p:RuntimeFrameworkVersion=10.0.14 --source https://api.nuget.org/v3/index.json -o $publish
    if($LASTEXITCODE){throw 'Self-contained publish failed'}
    $notices=Join-Path $publish 'Licenses'
    New-Item -ItemType Directory -Force $notices | Out-Null
    foreach($package in @('microsoft.netcore.app.runtime.win-x64','microsoft.windowsdesktop.app.runtime.win-x64')){
        $packageRoot=Join-Path $env:NUGET_PACKAGES "$package/10.0.14"
        foreach($file in Get-ChildItem -LiteralPath $packageRoot -File | Where-Object {$_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$'}){
            Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $notices ($package+'.'+$file.Name)) -Force
        }
    }
    $lock=Get-Content installer/compiler.lock.json -Raw | ConvertFrom-Json
    $compiler=Join-Path $repo '.tools/inno/ISCC.exe'
    if(!(Test-Path $compiler)){
        $download=Join-Path $repo ('.tools/downloads/innosetup-'+$lock.version+'-x64.exe')
        $ProgressPreference='SilentlyContinue'
        if(!(Test-Path $download)){Invoke-WebRequest -UseBasicParsing $lock.url -OutFile $download}
        if((Get-FileHash $download -Algorithm SHA256).Hash -ne $lock.sha256){throw 'Installer compiler checksum mismatch'}
        $dir=Join-Path $repo '.tools/inno'
        $p=Start-Process $download -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/PORTABLE=1',('/DIR="'+$dir+'"') -WindowStyle Hidden -PassThru
        $p.WaitForExit();if($p.ExitCode){throw 'Portable installer compiler setup failed'}
    }
    $arguments=@('/Qp',('/DAppVersion='+$version),('/DPublishDir='+$publish))
    if($TestInstall){$arguments+='/DTestInstall=1';$arguments+='/FAxiWidgets-Installer-Test'}
    & $compiler @arguments installer/AxiWidgets.iss
    if($LASTEXITCODE){throw 'Installer compilation failed'}
    Write-Host 'Installer ready in artifacts/installer.'
} finally {Pop-Location}
