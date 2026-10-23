# Shared verbatim across the three published Schedule I mod repositories.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RepositoryRoot,
    [string]$GameDir,
    [string]$Dotnet,
    [string]$CommitMessage,
    [switch]$PrepareOnly,
    [switch]$FromHook
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($FromHook -and $env:SCHEDULE_ONE_PUBLISH_ACTIVE -eq '1') { return }
if ($PrepareOnly -and $CommitMessage) { throw 'PrepareOnly never creates a commit. Omit CommitMessage.' }
function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments)][string[]]$Arguments)
    $output = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments[0]) failed: $($output -join [Environment]::NewLine)" }
    return ($output -join "`n").Trim()
}
function Get-ReleaseVersion([string]$XmlText) {
    $value = [string]([xml]$XmlText).Project.PropertyGroup.Version
    if ($value -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw 'Directory.Build.props must define one stable major.minor.patch Version.' }
    return $value
}
function Confirm-Package([string]$ZipPath, $Configuration, [string]$Version) {
    if (!(Test-Path -LiteralPath ($ZipPath + '.sha256'))) { throw 'Missing package checksum.' }
    $digest = (Get-FileHash -LiteralPath $ZipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $sidecar = (Get-Content -LiteralPath ($ZipPath + '.sha256') -Raw).Trim()
    if ($sidecar -cne "$digest  $([IO.Path]::GetFileName($ZipPath))") { throw 'Package SHA-256 sidecar does not match its ZIP.' }
    $archive = [IO.Compression.ZipFile]::OpenRead($ZipPath)
    try {
        $files = @($archive.Entries | Where-Object Name | ForEach-Object { $_.FullName.Replace('\','/') })
        if ($files.Count -ne @($files | Select-Object -Unique).Count -or (Compare-Object ($Configuration.files | Sort-Object) ($files | Sort-Object))) { throw 'Package file allowlist mismatch; release aborted.' }
        if (($archive.Entries | Measure-Object Length -Sum).Sum -gt 64MB) { throw 'Package exceeds the release size limit.' }
        function Read-Entry([string]$Name) {
            $entry = $archive.Entries | Where-Object { $_.FullName.Replace('\','/') -eq $Name } | Select-Object -First 1
            $reader = [IO.StreamReader]::new($entry.Open())
            try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
        }
        $manifest = Read-Entry 'manifest.json' | ConvertFrom-Json
        if ($manifest.name -ne $Configuration.packageName -or $manifest.version_number -ne $Version) { throw 'Manifest identity/version does not match Directory.Build.props.' }
        $packagedCompatibility = Read-Entry 'compatibility.json' | ConvertFrom-Json | ConvertTo-Json -Depth 30 -Compress
        $sourceCompatibility = Get-Content packaging/compatibility.json -Raw | ConvertFrom-Json | ConvertTo-Json -Depth 30 -Compress
        if ($packagedCompatibility -cne $sourceCompatibility) { throw 'Packaged compatibility differs from the source being released.' }
    } finally { $archive.Dispose() }
    return $digest
}
function Get-GitHubCredential {
    # Use the same configured credential helper as git push. Capture stdout in
    # memory; never display the helper output, persist the token or put it in argv.
    if ($env:GH_TOKEN) { return $env:GH_TOKEN }
    if ($env:GITHUB_TOKEN) { return $env:GITHUB_TOKEN }
    $start = [Diagnostics.ProcessStartInfo]::new('git')
    $start.ArgumentList.Add('credential'); $start.ArgumentList.Add('fill')
    $start.WorkingDirectory = $RepositoryRoot
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.Environment['GIT_TERMINAL_PROMPT'] = '0'; $start.Environment['GCM_INTERACTIVE'] = 'Never'
    $process = [Diagnostics.Process]::Start($start)
    try {
        $process.StandardInput.Write("protocol=https`nhost=github.com`npath=$($config.repository).git`n`n")
        $process.StandardInput.Close()
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (!$process.WaitForExit(30000)) { $process.Kill($true); throw 'Git credential helper timed out.' }
        $credential = $stdout.GetAwaiter().GetResult()
        $null = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw 'Git credential helper could not supply an existing login. Sign in with Git Credential Manager, or set GH_TOKEN for this process.' }
        foreach ($line in $credential.Split("`n")) { if ($line.StartsWith('password=')) { return $line.Substring(9).TrimEnd("`r") } }
        throw 'No GitHub credential was returned by the configured Git helper.'
    } finally { $process.Dispose(); $credential = $null; $stdout = $null }
}
function Invoke-GitHub([string]$Method, [string]$Path, $Body = $null, [string]$File = '', [switch]$AllowMissing) {
    $hostName = if ($File) { 'uploads.github.com' } else { 'api.github.com' }
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::new($Method), "https://$hostName/repos/$($config.repository)$Path")
    try {
        if ($File) { $request.Content = [Net.Http.StreamContent]::new([IO.File]::OpenRead($File)); $request.Content.Headers.ContentType = [Net.Http.Headers.MediaTypeHeaderValue]::new('application/octet-stream') }
        elseif ($null -ne $Body) { $request.Content = [Net.Http.StringContent]::new(($Body | ConvertTo-Json -Depth 30 -Compress), [Text.Encoding]::UTF8, 'application/json') }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            if ($AllowMissing -and [int]$response.StatusCode -eq 404) { return $null }
            $text = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if (!$response.IsSuccessStatusCode) {
                $message = try { ($text | ConvertFrom-Json).message } catch { 'No JSON error message' }
                throw "GitHub $Method returned HTTP $([int]$response.StatusCode): $message. No credentials were written to disk."
            }
            if ($text) { return $text | ConvertFrom-Json }
        } finally { $response.Dispose() }
    } finally { $request.Dispose() }
}
$client = $null; $anonymous = $null; $lock = $null
$oldGuard = $env:SCHEDULE_ONE_PUBLISH_ACTIVE
Push-Location $RepositoryRoot
try {
    $branch = & git symbolic-ref --quiet --short HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { if ($FromHook) { return }; throw 'Publish from the main branch, not a detached HEAD.' }
    if ($branch -ne 'main') { if ($FromHook) { return }; throw 'Publish from main only; merge the intended source first.' }
    $version = Get-ReleaseVersion (Get-Content Directory.Build.props -Raw)
    $config = Get-Content scripts/release-config.json -Raw | ConvertFrom-Json
    if ($FromHook) {
        # First installation of the version file does not count as a bump.
        $previousText = & git show HEAD^:Directory.Build.props 2>$null
        if ($LASTEXITCODE -ne 0) { Write-Host 'Release hook: no earlier central version; use publish.ps1 explicitly for an unpublished initial release.'; return }
        $previousVersion = Get-ReleaseVersion ($previousText -join "`n")
        if ($previousVersion -eq $version) { return }
        if ([version]$version -le [version]$previousVersion) { throw "Version must increase: $previousVersion -> $version." }
    }
    $notes = [Collections.Generic.List[string]]::new(); $collect = $false
    foreach ($line in Get-Content -LiteralPath $config.changelog) {
        if ($line -match ('^##\s+' + [regex]::Escape($version) + '(\s|$)')) { $collect = $true; continue }
        if ($collect -and $line -match '^##\s+') { break }
        if ($collect) { $notes.Add($line) }
    }
    if (!$collect) { throw "Add a $version section to $($config.changelog) before publishing." }
    $gitDirectory = Invoke-Git rev-parse --absolute-git-dir
    $lockPath = Join-Path $gitDirectory 'schedule-one-publish.lock'
    try { $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch { throw 'Another release command is already running for this repository.' }
    $env:SCHEDULE_ONE_PUBLISH_ACTIVE = '1'
    if ($CommitMessage) {
        if (Invoke-Git diff --name-only) { throw 'Stage all intended changes before using CommitMessage; unstaged changes were preserved.' }
        if (Invoke-Git ls-files --others --exclude-standard) { throw 'Stage or ignore new files before committing a release.' }
        if (!(Invoke-Git diff --cached --name-only)) { throw 'No staged changes to commit.' }
        Write-Host (Invoke-Git commit -m $CommitMessage)
    }
    if (!$PrepareOnly -and (Invoke-Git status --porcelain)) { throw 'Release requires a clean working tree. Commit the intended files first, or stage them and use -CommitMessage.' }
    $commit = Invoke-Git rev-parse HEAD
    $origin = Invoke-Git remote get-url origin
    $expectedHttps = "https://github.com/$($config.repository)"
    if ($origin.TrimEnd('/').Replace('.git','') -notin @($expectedHttps, "git@github.com:$($config.repository)")) { throw 'origin does not match the configured release repository.' }
    if (!$GameDir) { $GameDir = $env:SCHEDULE_I_GAME_DIR }
    if (!$GameDir) { $GameDir = & git config --local --get scheduleone.gameDir }
    if (!$GameDir) { $GameDir = 'Q:\Games\Schedule I' }
    if (!$Dotnet) { $Dotnet = $env:SCHEDULE_ONE_DOTNET }
    if (!$Dotnet) { $Dotnet = & git config --local --get scheduleone.dotnet }
    if (!$Dotnet) { $Dotnet = 'dotnet' }
    $GameDir = (Resolve-Path -LiteralPath $GameDir).Path
    if (!(Test-Path -LiteralPath (Join-Path $GameDir 'MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll'))) { throw 'GameDir must contain the generated IL2CPP build references. No game launch is performed.' }
    $tag = "v$version"
    $release = $null
    if (!$PrepareOnly) {
        $token = Get-GitHubCredential
        $client = [Net.Http.HttpClient]::new(); $client.Timeout = [TimeSpan]::FromSeconds(90)
        $client.DefaultRequestHeaders.Authorization = [Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer', $token)
        $token = $null
        $client.DefaultRequestHeaders.UserAgent.ParseAdd('ScheduleOneMods-LocalPublisher/1.0')
        $client.DefaultRequestHeaders.Add('Accept','application/vnd.github+json')
        $client.DefaultRequestHeaders.Add('X-GitHub-Api-Version','2022-11-28')
        $repository = Invoke-GitHub GET ''
        if (!$repository.permissions.push) { throw 'The configured GitHub login cannot publish to this repository.' }
        $release = Invoke-GitHub GET "/releases/tags/$tag" -AllowMissing
        if ($release -and !$release.draft) {
            Write-Host "$tag is already published: $($release.html_url). Published assets are never replaced. Bump Directory.Build.props for a new release."
            return
        }
        $latest = Invoke-GitHub GET '/releases/latest' -AllowMissing
        if ($latest -and $latest.tag_name -match '^v(\d+\.\d+\.\d+)$' -and [version]$version -le [version]$Matches[1]) { throw 'The release version must be higher than the latest published version.' }
    }
    # A retry reuses the verified candidate for this exact source commit.
    # PrepareOnly uses a fresh folder because it may include uncommitted work.
    $candidateId = if ($PrepareOnly) { [guid]::NewGuid().ToString('N') } else { $commit }
    $output = Join-Path $RepositoryRoot "artifacts/release-candidates/$candidateId"
    $zip = Join-Path $output "$($config.packageName)-$version-IL2CPP.zip"
    $marker = Join-Path $output 'source-commit.txt'
    $initialStatus = Invoke-Git status --porcelain
    if (!(Test-Path -LiteralPath $marker)) {
        if (Test-Path -LiteralPath $zip) { throw 'An incomplete candidate exists. Preserve/inspect it, then remove its candidate folder before retrying.' }
        & (Join-Path $RepositoryRoot 'package.ps1') -GameDir $GameDir -Dotnet $Dotnet -OutputDirectory $output
        if ($LASTEXITCODE -ne 0) { throw 'Package command failed.' }
        if ((Invoke-Git rev-parse HEAD) -ne $commit -or (Invoke-Git status --porcelain) -cne $initialStatus) { throw 'Source changed while packaging; no release was published.' }
        $null = Confirm-Package $zip $config $version
        Set-Content -LiteralPath $marker -Value $commit -Encoding ascii
    }
    if ((Get-Content -LiteralPath $marker -Raw).Trim() -ne $commit) { throw 'Candidate source commit mismatch.' }
    $digest = Confirm-Package $zip $config $version
    Write-Host "Verified $([IO.Path]::GetFileName($zip)): $digest"
    if ($PrepareOnly) { Write-Host "Prepared only: $zip. No commit, push, credential lookup or release request performed."; return }
    if ((Invoke-Git rev-parse HEAD) -ne $commit -or (Invoke-Git status --porcelain)) { throw 'Source changed before publishing; release aborted.' }
    $localTag = & git rev-parse --verify "refs/tags/$tag^{commit}" 2>$null
    if ($LASTEXITCODE -eq 0) { if ($localTag -ne $commit) { throw 'Local release tag already points to another commit; tags are never moved.' } }
    else { $null = Invoke-Git tag -a $tag -m "$($config.displayName) $version" $commit }
    # Atomically push the exact source and tag. Refuse divergence; never force.
    Write-Host (Invoke-Git push --atomic origin 'HEAD:refs/heads/main' "refs/tags/$tag")
    if (!$release) { $release = Invoke-GitHub POST '/releases' @{tag_name=$tag; target_commitish=$commit; name="$($config.displayName) $version"; body=($notes -join "`n").Trim(); draft=$true; prerelease=$false} }
    $assets = @(Invoke-GitHub GET "/releases/$($release.id)/assets")
    $anonymous = [Net.Http.HttpClient]::new(); $anonymous.Timeout = [TimeSpan]::FromSeconds(90)
    $anonymous.DefaultRequestHeaders.UserAgent.ParseAdd('ScheduleOneMods-ReleaseVerifier/1.0')
    foreach ($file in @($zip, ($zip + '.sha256'))) {
        $name = [IO.Path]::GetFileName($file)
        $existing = $assets | Where-Object name -eq $name | Select-Object -First 1
        if ($existing) {
            # GitHub exposes SHA-256 asset digests; never replace an uploaded asset.
            $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($existing.digest -ne "sha256:$hash") { throw "Draft asset '$name' differs from the candidate; it was preserved. Inspect the draft before retrying." }
        } else {
            $null = Invoke-GitHub POST "/releases/$($release.id)/assets?name=$([uri]::EscapeDataString($name))" -File $file
        }
    }
    # Confirm both remote digests before exposing the release to the manager.
    $assets = @(Invoke-GitHub GET "/releases/$($release.id)/assets")
    foreach ($file in @($zip, ($zip + '.sha256'))) {
        $asset = $assets | Where-Object name -eq ([IO.Path]::GetFileName($file)) | Select-Object -First 1
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        if (!$asset -or $asset.digest -ne "sha256:$hash") { throw 'Remote asset digest verification failed; the release remains a draft.' }
    }
    $release = Invoke-GitHub PATCH "/releases/$($release.id)" @{draft=$false; make_latest='true'}
    $asset = $release.assets | Where-Object name -eq ([IO.Path]::GetFileName($zip)) | Select-Object -First 1
    $bytes = $anonymous.GetByteArrayAsync($asset.browser_download_url).GetAwaiter().GetResult()
    $publicDigest = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    if ($publicDigest -ne $digest) { throw 'Published download verification failed. Inspect the release; no asset was replaced.' }
    $latest = Invoke-GitHub GET '/releases/latest'
    if ($latest.tag_name -ne $tag) { throw 'The release is published, but GitHub Latest does not point to it. Inspect release ordering.' }
    Write-Host "Published and verified: $($release.html_url)"
} finally {
    if ($client) { $client.Dispose() }; if ($anonymous) { $anonymous.Dispose() }
    if ($lock) { $lock.Dispose() }
    $env:SCHEDULE_ONE_PUBLISH_ACTIVE = $oldGuard
    Pop-Location
}
