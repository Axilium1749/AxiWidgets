param([string]$DownloadCache='')
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
if($env:OS -ne 'Windows_NT' -or ![Environment]::Is64BitProcess){throw 'Use 64-bit PowerShell on Windows.'}
$repo=Split-Path $PSScriptRoot -Parent
$toolRoot=Join-Path $repo '.tools'
$env:DOTNET_CLI_HOME=Join-Path $toolRoot 'home'
$env:NUGET_PACKAGES=Join-Path $toolRoot 'nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
if(!$DownloadCache){$DownloadCache=Join-Path $toolRoot 'downloads'}
$DownloadCache=[IO.Path]::GetFullPath($DownloadCache)
$lock=Get-Content (Join-Path $PSScriptRoot 'toolchain.lock.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force $toolRoot,$DownloadCache | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
[Net.ServicePointManager]::SecurityProtocol=[Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

function Expand-ToolArchive([string]$Archive,[string]$Destination){
    $root=[IO.Path]::GetFullPath($Destination).TrimEnd('\')+'\'
    New-Item -ItemType Directory -Force $root | Out-Null
    $zip=[IO.Compression.ZipFile]::OpenRead($Archive)
    try {
        foreach($entry in $zip.Entries){
            $target=[IO.Path]::GetFullPath([IO.Path]::Combine($root,$entry.FullName))
            if(!$target.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe archive entry.'}
            if(!$entry.Name){[void][IO.Directory]::CreateDirectory($target);continue}
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$true)
        }
    }finally{$zip.Dispose()}
}
function Install-ToolPackage($Package){
    $url=[uri]$Package.url
    if($url.Scheme -ne 'https' -or $url.Host -notin @('builds.dotnet.microsoft.com','download.visualstudio.microsoft.com','api.nuget.org')){throw 'Unexpected tool download origin.'}
    $archive=Join-Path $DownloadCache $Package.file
    if(!(Test-Path -LiteralPath $archive)){
        Write-Host "Downloading $($Package.file)..."
        $partial=$archive+'.partial'
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $partial
        Move-Item -LiteralPath $partial -Destination $archive -Force
    }
    if((Get-FileHash -LiteralPath $archive -Algorithm $Package.algorithm).Hash -ne $Package.hash){throw "Checksum mismatch: $($Package.file). Remove this cached archive and retry."}
    Write-Host "Preparing $($Package.file)..."
    Expand-ToolArchive $archive (Join-Path $toolRoot $Package.destination)
}

$dotnet=Join-Path $toolRoot 'dotnet/dotnet.exe'
$installedVersion=''
if(Test-Path -LiteralPath $dotnet){$installedVersion=(& $dotnet --version | Out-String).Trim()}
if($installedVersion -ne $lock.dotnet.version){Install-ToolPackage $lock.dotnet}
foreach($package in $lock.packages){Install-ToolPackage $package}
if((& $dotnet --version | Out-String).Trim() -ne $lock.dotnet.version){throw 'The .NET SDK version does not match the lock file.'}
Write-Host 'Toolchain ready in .tools. No global SDK, PATH or system settings were changed.'
