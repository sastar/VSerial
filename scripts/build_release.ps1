[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$Version = "",
    [switch]$BuildPortable = $false,
    [switch]$SkipInstaller = $false
)

$ErrorActionPreference = "Stop"

$RootDir = (Get-Item "$PSScriptRoot\..").FullName
$BuildDir = Join-Path $RootDir "build"
$StagingDir = Join-Path $BuildDir "staging"
$DistDir = Join-Path $RootDir "dist"
$InstallerScript = Join-Path $RootDir "installer\vserial_setup.iss"

# Resolve version from Directory.Build.props if not explicitly passed
if ([string]::IsNullOrWhiteSpace($Version)) {
    $PropsFile = Join-Path $RootDir "Directory.Build.props"
    if (Test-Path $PropsFile) {
        [xml]$propsXml = Get-Content -Path $PropsFile -Raw
        $Version = $propsXml.Project.PropertyGroup.Version
    }
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = "2.0.0"
}

# Locate dotnet
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    throw "dotnet CLI not found. Please install .NET 8.0 SDK."
}

# Locate MSBuild
$MsBuildCandidates = @(
    "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\amd64\MSBuild.exe",
    "C:\Program Files (x86)\Microsoft Visual Studio\2019\Professional\MSBuild\Current\Bin\amd64\MSBuild.exe"
)

$MsBuildPath = $null
foreach ($path in $MsBuildCandidates) {
    if (Test-Path $path) {
        $MsBuildPath = $path
        break
    }
}

if (-not $MsBuildPath) {
    $cmd = Get-Command "MSBuild.exe" -ErrorAction SilentlyContinue
    if ($cmd) {
        $MsBuildPath = $cmd.Source
    }
}

if (-not $MsBuildPath) {
    throw "MSBuild.exe not found. Please ensure Visual Studio with WDK is installed."
}

# Locate Inno Setup
$IsccCandidates = @(
    "C:\Program Files (x86)\Inno Setup 6\iscc.exe",
    "C:\Program Files\Inno Setup 6\iscc.exe",
    "C:\Program Files (x86)\Inno Setup 5\iscc.exe"
)

$IsccPath = $null
foreach ($path in $IsccCandidates) {
    if (Test-Path $path) {
        $IsccPath = $path
        break
    }
}

if (-not $IsccPath) {
    $cmd = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
    if ($cmd) {
        $IsccPath = $cmd.Source
    }
}

# Prepare directories
if (Test-Path $StagingDir) {
    Remove-Item $StagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

if (-not (Test-Path $DistDir)) {
    New-Item -ItemType Directory -Path $DistDir -Force | Out-Null
}

# 1. Build driver
Write-Host "Building driver..."
$DriverProj = Join-Path $RootDir "src\Driver\VirtualSerial2um\VirtualSerial2um.vcxproj"
$DriverBuildArgs = @(
    $DriverProj,
    "/p:Configuration=$Configuration",
    "/p:Platform=$Platform",
    "/m",
    "/v:minimal"
)

& $MsBuildPath @DriverBuildArgs
if ($LASTEXITCODE -ne 0) {
    throw "Driver build failed."
}

$DriverOutDir = Join-Path $RootDir "src\Driver\VirtualSerial2um\x64\$Configuration\VirtualSerial2um"
$InstallerDriverDir = Join-Path $RootDir "installer\driver"
if (Test-Path $DriverOutDir) {
    Copy-Item "$DriverOutDir\*" -Destination $InstallerDriverDir -Recurse -Force
}

# 2. Publish CLI
Write-Host "Publishing CLI..."
$CliProj = Join-Path $RootDir "src\CLI\VSerial.Cli\VSerial.Cli.csproj"
$CliPublishArgs = @(
    "publish", $CliProj,
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "false",
    "-p:PublishSingleFile=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-o", $StagingDir
)

& dotnet @CliPublishArgs
if ($LASTEXITCODE -ne 0) {
    throw "VSerial.Cli publish failed."
}

# 3. Publish UI
Write-Host "Publishing UI..."
$UiProj = Join-Path $RootDir "src\UI\VSerial.UI\VSerial.UI.csproj"
$UiPublishArgs = @(
    "publish", $UiProj,
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "false",
    "-p:PublishSingleFile=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-o", $StagingDir
)

& dotnet @UiPublishArgs
if ($LASTEXITCODE -ne 0) {
    throw "VSerial.UI publish failed."
}

# Stage driver files
$DriverStaging = Join-Path $StagingDir "driver"
if (-not (Test-Path $DriverStaging)) {
    New-Item -ItemType Directory -Path $DriverStaging -Force | Out-Null
}
Copy-Item "$InstallerDriverDir\*" -Destination $DriverStaging -Recurse -Force

# Clean root driver artifacts
$RootDriverFiles = @("VirtualSerial2um.cer", "VirtualSerial2um.dll", "virtualserial2um.inf", "wudf.cat")
foreach ($f in $RootDriverFiles) {
    $leakPath = Join-Path $StagingDir $f
    if (Test-Path $leakPath) {
        Remove-Item $leakPath -Force
    }
}

# Clean and assert no pdb files
$PdbFiles = Get-ChildItem -Path $StagingDir -Recurse -Filter "*.pdb" -ErrorAction SilentlyContinue
if ($PdbFiles) {
    $PdbFiles | Remove-Item -Force
}

$RemainingPdbs = Get-ChildItem -Path $StagingDir -Recurse -Filter "*.pdb" -ErrorAction SilentlyContinue
if ($RemainingPdbs) {
    throw "Unresolved PDB files found in staging."
}

$RootJsonFiles = Get-ChildItem -Path $StagingDir -File -Filter "*.json" -ErrorAction SilentlyContinue
if ($RootJsonFiles) {
    throw "Unexpected JSON files found in staging root: $($RootJsonFiles.Name -join ', ')"
}

$RootDllFiles = Get-ChildItem -Path $StagingDir -File -Filter "*.dll" -ErrorAction SilentlyContinue
if ($RootDllFiles) {
    throw "Unexpected DLL files found in staging root: $($RootDllFiles.Name -join ', ')"
}

foreach ($df in $RootDriverFiles) {
    $expectedPath = Join-Path $DriverStaging $df
    if (-not (Test-Path $expectedPath)) {
        throw "Missing driver file in staging: $df"
    }
}

# 4. Package portable archive (optional, off by default)
if ($BuildPortable) {
    Write-Host "Creating portable archive..."
    $PortableZip = Join-Path $DistDir "VSerial-v$Version-Portable-x64.zip"
    if (Test-Path $PortableZip) {
        Remove-Item $PortableZip -Force
    }
    Compress-Archive -Path "$StagingDir\*" -DestinationPath $PortableZip -CompressionLevel Optimal
}

# 5. Build installer (default)
if ($IsccPath -and (-not $SkipInstaller)) {
    Write-Host "Building installer (v$Version)..."
    & $IsccPath "/DMyAppVersion=$Version" $InstallerScript
    if ($LASTEXITCODE -ne 0) {
        throw "Installer compilation failed."
    }
} elseif (-not $SkipInstaller) {
    Write-Warning "Inno Setup (iscc.exe) not found. Skipping installer build."
}

# Summary
Write-Host "`nArtifacts:" -ForegroundColor Cyan
$Outputs = Get-ChildItem -Path $DistDir -File | Where-Object { $_.Name -like "*$Version*" }
foreach ($item in $Outputs) {
    $sizeMb = [math]::Round($item.Length / 1MB, 2)
    Write-Host "  $($item.Name) ($sizeMb MB)"
}
