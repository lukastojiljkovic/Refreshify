#Requires -Version 5.1
<#
.SYNOPSIS
    Tests Refreshify, publishes a self-contained x64 build with its third-party licenses and compiles the Inno Setup
    installer.
.OUTPUTS
    artifacts\installer\Refreshify-<version>-Setup.exe
#>
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Invoke-Step([string] $name, [scriptblock] $command) {
    Write-Host "==> $name" -ForegroundColor Cyan
    & $command
    if ($LASTEXITCODE) { throw "$name failed with exit code $LASTEXITCODE." }
}

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup 6 was not found. Install it with: winget install JRSoftware.InnoSetup' }

$version = ([xml](Get-Content "$root\Directory.Build.props")).Project.PropertyGroup.Version
$publishDir = "$root\artifacts\publish\win-x64"

# global.json, which selects the Microsoft.Testing.Platform test runner, is found from the working directory.
Invoke-Step 'Test' { Push-Location $root; try { dotnet test --project tests\Refreshify.Core.Tests -c Release } finally { Pop-Location } }

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
Invoke-Step 'Publish' { dotnet publish "$root\src\Refreshify\Refreshify.csproj" -c Release -r win-x64 -p:Platform=x64 -p:PublishReadyToRun=true -o $publishDir --nologo }

# The self-contained build redistributes the .NET runtime and the Windows App SDK, whose licenses and notices must ship with it.
Write-Host '==> Collect third-party licenses' -ForegroundColor Cyan
$packages = (dotnet nuget locals global-packages --list) -replace '^global-packages:\s*', ''
$licenseDir = New-Item -ItemType Directory -Force "$publishDir\licenses"
$libraries = (Get-Content "$publishDir\Refreshify.deps.json" -Raw | ConvertFrom-Json).libraries.PSObject.Properties.Name
foreach ($library in $libraries -match '^(runtimepack\.Microsoft\.NETCore\.App\.Runtime\.|Microsoft\.WindowsAppSDK\.)') {
    $name, $libraryVersion = ($library -replace '^runtimepack\.', '') -split '/'
    Get-ChildItem "$packages\$($name.ToLowerInvariant())\$libraryVersion" -File |
        Where-Object Name -match '^(license|notice|third-party-notices)\.txt$' |
        ForEach-Object { Copy-Item $_.FullName "$licenseDir\$name.$($_.Name.ToUpperInvariant())" }
}
if (-not (Get-ChildItem $licenseDir)) { throw 'No third-party license files were found in the NuGet package cache.' }

Invoke-Step 'Installer' { & $iscc /Q "/DAppVersion=$version" "$root\installer\Refreshify.iss" }

Write-Host "Installer: $root\artifacts\installer\Refreshify-$version-Setup.exe" -ForegroundColor Green
