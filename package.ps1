param([string]$GameDir = 'Q:\Games\Schedule I')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1') -GameDir $GameDir
$dll = Join-Path $PSScriptRoot 'artifacts/SpecialMixingTerminal.dll'
Add-Type -Path (Join-Path $GameDir 'MelonLoader/net6/Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll)
try {
    $info = $assembly.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'MelonLoader.MelonInfoAttribute' }
    if (!$info) { throw 'Missing MelonInfo.' }
    $name = [string]$info.ConstructorArguments[1].Value
    $version = [string]$info.ConstructorArguments[2].Value
    $author = [string]$info.ConstructorArguments[3].Value
    if ($assembly.MainModule.Types.FullName -match 'DevTools|PlacementProbe|NativeAudit') { throw 'Development tooling found in production assembly.' }
    if ($assembly.MainModule.AssemblyReferences.Name -match 'DevTools') { throw 'Production references development assembly.' }
    if ($assembly.MainModule.Resources.Name -notcontains 'SpecialMixingTerminal.TerminalIcon.png') { throw 'Embedded inventory icon is missing.' }
} finally { $assembly.Dispose() }
$project = [xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'SpecialMixingTerminal.csproj') -Raw)
if ($name -ne 'Special Mixing Terminal' -or $author -ne 'Dekode1859' -or $version -notmatch '^\d+\.\d+\.\d+$' -or $project.Project.PropertyGroup.Version -ne $version) {
    throw 'Compiled mod identity/version does not match the project.'
}
$stage = Join-Path $PSScriptRoot ('artifacts/package-staging/' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $stage 'Mods') -Force | Out-Null
Copy-Item -LiteralPath $dll -Destination (Join-Path $stage 'Mods/SpecialMixingTerminal.dll')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md'),(Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'packaging/CHANGELOG.md'),(Join-Path $PSScriptRoot 'packaging/compatibility.json') -Destination $stage
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Assets/terminal-icon.png') -Destination (Join-Path $stage 'icon.png')
$manifest = [ordered]@{
    name = 'SpecialMixingTerminal'
    version_number = $version
    website_url = 'https://github.com/schedule-one-mods/schedule-one-special-mixing-terminal'
    description = 'Program one to four native mixing stations from a purchasable terminal, with clipboard equipment selection, one handler and one chemist.'
    dependencies = @('LavaGang-MelonLoader-0.7.3')
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding utf8
$release = Join-Path $PSScriptRoot 'artifacts/releases'
New-Item -ItemType Directory -Path $release -Force | Out-Null
$zip = Join-Path $release "SpecialMixingTerminal-$version-IL2CPP.zip"
if (Test-Path -LiteralPath $zip) { throw 'Package exists. Published assets are immutable; use a new version or a fresh artifact directory.' }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    $expected = @('manifest.json','compatibility.json','README.md','CHANGELOG.md','THIRD-PARTY-NOTICES.md','icon.png','Mods/SpecialMixingTerminal.dll') | Sort-Object
    $actual = @($archive.Entries | Where-Object { $_.Name } | ForEach-Object { $_.FullName.Replace('\','/') }) | Sort-Object
    if (Compare-Object $expected $actual) { throw 'Unexpected package files; production allowlist check failed.' }
} finally { $archive.Dispose() }
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ascii
Write-Host "Package: $zip"
Write-Host "Compiled version: $version; production-only file list verified."
