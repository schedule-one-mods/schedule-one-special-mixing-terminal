param([string]$GameDir = 'Q:\Games\Schedule I', [switch]$Deploy, [string]$Dotnet)
$ErrorActionPreference = 'Stop'
$sdk = Join-Path (Split-Path -Parent $PSScriptRoot) 'tools\dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $sdk)) { $sdk = 'dotnet' }
if ($Dotnet) { $sdk = $Dotnet }
& $sdk build (Join-Path $PSScriptRoot 'SpecialMixingTerminal.csproj') -c Release --nologo -v:q "-p:GameDir=$GameDir" -p:NoWarn=MSB3277
if ($LASTEXITCODE -ne 0) { throw 'Special Mixing Terminal build failed.' }
$output = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\net6.0\SpecialMixingTerminal.dll') -Destination $output
if ($Deploy) {
    if (Get-Process -Name 'Schedule I' -ErrorAction SilentlyContinue) { throw 'Close Schedule I before deploying.' }
    Copy-Item -LiteralPath (Join-Path $output 'SpecialMixingTerminal.dll') -Destination (Join-Path $GameDir 'Mods\SpecialMixingTerminal.dll')
}
Write-Host "Built: $output\SpecialMixingTerminal.dll"
