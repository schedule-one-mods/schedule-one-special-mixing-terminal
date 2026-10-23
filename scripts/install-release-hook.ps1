param([switch]$Remove, [string]$GameDir, [string]$Dotnet)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot
try {
    $configured = git config --local --get core.hooksPath
    if ($LASTEXITCODE -notin @(0,1)) { throw 'Cannot read local Git hook settings.' }
    if ($configured -and $configured -ne '.githooks') { throw "Existing hook path '$configured' was preserved. Chain the release hook manually instead of replacing it." }
    $oldHook = git rev-parse --git-path hooks/post-commit
    if (!$Remove -and !$configured -and (Test-Path -LiteralPath $oldHook)) { throw 'Existing post-commit hook was preserved. Chain the release hook manually.' }
    if ($Remove) {
        if ($configured -eq '.githooks') { git config --local --unset core.hooksPath }
        Write-Host 'Automatic release hook disabled. publish.ps1 remains available.'
    } else {
        if (!(Get-Command pwsh -ErrorAction SilentlyContinue)) { throw 'Install PowerShell 7 (pwsh) before enabling the hook.' }
        git config --local scheduleone.pwsh ((Get-Command pwsh).Source.Replace('\','/'))
        if ($GameDir) { git config --local scheduleone.gameDir $GameDir }
        if ($Dotnet) { git config --local scheduleone.dotnet $Dotnet }
        git config --local core.hooksPath .githooks
        if ($LASTEXITCODE -ne 0) { throw 'Could not install repository-local release hook.' }
        Write-Host 'Enabled locally: a version increase committed on main runs publish.ps1. Other commits/branches are ignored.'
    }
} finally { Pop-Location }
