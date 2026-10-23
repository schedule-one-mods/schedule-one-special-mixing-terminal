param(
    [string]$GameDir,
    [string]$Dotnet,
    [string]$CommitMessage,
    [switch]$PrepareOnly,
    [switch]$FromHook
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'scripts/publish-mod.ps1') -RepositoryRoot $PSScriptRoot @PSBoundParameters
