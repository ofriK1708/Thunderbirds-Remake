<#
.SYNOPSIS
    Checks the collaboration rules in docs/COLLABORATION.md before a PR to main.

.EXAMPLE
    pwsh ./tools/check-rules.ps1
    pwsh ./tools/check-rules.ps1 -Base origin/main -Actor rotem444
#>
[CmdletBinding()]
param(
    [string]$Base,
    [string]$Actor,
    # Audit the whole repo instead of only the files this branch changed.
    [switch]$All
)

$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------- setup ----
$repoRoot = (git rev-parse --show-toplevel).Trim()
Set-Location $repoRoot

# Project folder = the one holding ProjectSettings/ProjectVersion.txt
$versionFile = (git ls-files '*ProjectSettings/ProjectVersion.txt' | Select-Object -First 1)
if (-not $versionFile) { Write-Host 'Could not find the Unity project folder.' -ForegroundColor Red; exit 2 }
$proj = (Split-Path (Split-Path $versionFile -Parent) -Parent) -replace '\\', '/'
$projPrefix = if ($proj -and $proj -ne '.') { "$proj/" } else { '' }

if (-not $Base) {
    $Base = if (git rev-parse --verify --quiet origin/main) { 'origin/main' } else { 'main' }
}
if (-not $Actor) {
    $Actor = $env:GITHUB_ACTOR
}
if (-not $Actor) {
    $who = "$(git config user.name) $(git config user.email)".ToLower()
    $Actor = if ($who -match 'rotem') { 'rotem444' } elseif ($who -match 'ofri') { 'ofriK1708' } else { 'unknown' }
}

$mergeBase = (git merge-base HEAD $Base 2>$null)
if (-not $mergeBase) { $mergeBase = $Base }
$changed = @(git diff --name-only "$mergeBase" HEAD) + @(git diff --name-only HEAD) +
           @(git diff --name-only --cached) + @(git ls-files --others --exclude-standard)
$changed = $changed | Where-Object { $_ } | Sort-Object -Unique
$baseChanged = @(git diff --name-only "$mergeBase" $Base 2>$null) | Where-Object { $_ }
$tracked = @(git ls-files)
# Content checks also look at new, not-yet-committed files (ignored files excluded).
$scanFiles = ($tracked + @(git ls-files --others --exclude-standard)) | Where-Object { $_ } | Sort-Object -Unique

Write-Host ""
Write-Host "Collaboration rule check" -ForegroundColor Cyan
Write-Host "  actor:   $Actor"
Write-Host "  base:    $Base"
Write-Host "  changed: $($changed.Count) file(s)"
Write-Host ""

$failures = 0
$warnings = 0
function Pass([string]$rule, [string]$msg) { Write-Host ("[OK]   {0,-4} {1}" -f $rule, $msg) -ForegroundColor Green }
function Fail([string]$rule, [string]$msg, $items) {
    $script:failures++
    Write-Host ("[FAIL] {0,-4} {1}" -f $rule, $msg) -ForegroundColor Red
    foreach ($i in @($items) | Select-Object -First 10) { Write-Host "         - $i" -ForegroundColor Red }
}
function Warn([string]$rule, [string]$msg, $items) {
    $script:warnings++
    Write-Host ("[WARN] {0,-4} {1}" -f $rule, $msg) -ForegroundColor Yellow
    foreach ($i in @($items) | Select-Object -First 10) { Write-Host "         - $i" -ForegroundColor Yellow }
}

function Get-Matches([string]$glob, [string]$pattern, [string[]]$excludeGlobs) {
    # Content rules judge the code this branch touches, not code that was already on main.
    # Use -All for a full-repo audit.
    $pool = if ($All -or -not $changed) { $scanFiles } else { $scanFiles | Where-Object { $changed -contains $_ } }
    $files = $pool | Where-Object { $_ -like $glob }
    foreach ($ex in $excludeGlobs) { $files = $files | Where-Object { $_ -notlike $ex } }
    $hits = @()
    foreach ($f in $files) {
        if (-not (Test-Path $f)) { continue }
        $m = Select-String -Path $f -Pattern $pattern -SimpleMatch:$false -ErrorAction SilentlyContinue
        foreach ($x in $m) { $hits += "$($x.Path):$($x.LineNumber): $($x.Line.Trim())" }
    }
    return $hits
}

# ------------------------------------------------------- R1 scene owners ----
$sceneOwners = @{ 'Game.unity' = 'ofriK1708'; 'MainMenu.unity' = 'rotem444' }
function Get-Handle([string]$who) {
    $who = $who.ToLower()
    if ($who -match 'rotem') { 'rotem444' } elseif ($who -match 'ofri') { 'ofriK1708' } else { 'unknown' }
}
$violations = @()
foreach ($f in $changed | Where-Object { $_ -like '*.unity' }) {
    $owner = $sceneOwners[[System.IO.Path]::GetFileName($f)]
    if (-not $owner) { continue }
    # Judge each commit by its author (a branch may carry the owner's own commits),
    # and uncommitted edits by whoever is running the check.
    $editors = @(git log --format='%an %ae' "$mergeBase..HEAD" -- $f | ForEach-Object { Get-Handle $_ })
    if (git status --porcelain -- $f) { $editors += $Actor }
    foreach ($e in $editors | Sort-Object -Unique) {
        if ($e -ne 'unknown' -and $e -ne $owner) { $violations += "$f (owner: $owner, edited by: $e)" }
    }
}
if ($violations) { Fail 'R1' 'A scene was changed by someone who does not own it' $violations }
else { Pass 'R1' 'scene ownership respected' }

# --------------------------------------- R3 same scene changed on both -----
$bothSides = @()
foreach ($f in $changed | Where-Object { $_ -like '*.unity' -or $_ -like '*.prefab' }) {
    if ($baseChanged -contains $f) { $bothSides += $f }
}
if ($bothSides) { Fail 'R3' "This branch and $Base both changed the same scene/prefab - rebase before opening the PR" $bothSides }
else { Pass 'R3' 'no scene/prefab changed on both sides' }

# ------------------------------------------------- R4 rules layer purity ----
$hits = Get-Matches "$projPrefix`Assets/Scripts/Rules/*.cs" 'using\s+UnityEngine|MonoBehaviour|ScriptableObject|Time\.deltaTime' @()
if ($hits) { Fail 'R4' 'Rules layer references Unity - it must stay plain C#' $hits }
else { Pass 'R4' 'rules layer is Unity-free' }

# ------------------------------------------------------- R5 direct input ----
$hits = Get-Matches "$projPrefix`Assets/Scripts/*.cs" 'Keyboard\.current|Input\.GetKey|Input\.GetAxis|Input\.GetButton' @("*Tests*")
if ($hits) { Fail 'R5' 'Direct key reads found - use the Input System action map' $hits }
else { Pass 'R5' 'input goes through action maps' }

# ---------------------------------------------------------- R6 singletons ----
$hits = Get-Matches "$projPrefix`Assets/Scripts/*.cs" 'static\s+\w+\s+Instance' @("*GameManager*", "*AudioManager*", "*Tests*")
if ($hits) { Fail 'R6' 'Singleton outside GameManager / AudioManager' $hits }
else { Pass 'R6' 'no unexpected singletons' }

# --------------------------------------------------------- R8 meta files ----
# Unity ignores dot-files and dot-folders (e.g. .gitkeep) and never gives them a .meta.
$assets = $tracked | Where-Object { $_ -like "$projPrefix`Assets/*" -and $_ -notmatch '/\.[^/]+(/|$)' }
$assetSet = [System.Collections.Generic.HashSet[string]]::new()
foreach ($a in $assets) { [void]$assetSet.Add($a) }
$missingMeta = @()
$orphanMeta = @()
foreach ($a in $assets) {
    if ($a -like '*.meta') {
        $target = $a.Substring(0, $a.Length - 5)
        if (-not $assetSet.Contains($target) -and -not (Test-Path $target)) { $orphanMeta += $a }
    }
    else {
        if (-not $assetSet.Contains("$a.meta")) { $missingMeta += $a }
    }
}
if ($missingMeta -or $orphanMeta) {
    Fail 'R8' 'Meta file problems (missing .meta breaks references on the other machine)' ($missingMeta + $orphanMeta)
}
else { Pass 'R8' 'every asset has its .meta' }

# -------------------------------------------------------------- R9 junk ----
$junk = $tracked | Where-Object {
    $_ -match '(^|/)(Library|Temp|Logs|Obj|Build|Builds)/' -or $_ -like '*.zip' -or $_ -match '(^|/)\.idea/'
}
if ($junk) { Fail 'R9' 'Generated or downloaded content is committed' $junk }
else { Pass 'R9' 'no generated/downloaded content committed' }

# --------------------------------------------------------------- R10 LFS ----
$notTracked = @()   # .gitattributes does not route this file to LFS -> future commits store it raw
$rawBlob = @()      # routed to LFS now, but the committed blob predates that rule
foreach ($img in $tracked | Where-Object { $_ -match '\.(png|jpg|jpeg|psd|aseprite)$' }) {
    $attr = (git check-attr filter -- "$img") 2>$null
    if ($attr -notmatch 'filter:\s*lfs') { $notTracked += $img; continue }
    $head = (git show "HEAD:$img" 2>$null | Select-Object -First 1)
    if ($head -and $head -notmatch 'git-lfs') { $rawBlob += $img }
}
if ($notTracked) { Fail 'R10' 'Images not routed to Git LFS by .gitattributes' $notTracked }
elseif ($rawBlob) { Warn 'R10' 'Images committed before the LFS rule existed (re-add them to migrate)' $rawBlob }
else { Pass 'R10' 'images stored in Git LFS' }

# ---------------------------------------------------- R11 Unity settings ----
$editorSettings = "$projPrefix`ProjectSettings/EditorSettings.asset"
if (Test-Path $editorSettings) {
    $text = Get-Content -LiteralPath $editorSettings -Raw
    $bad = @()
    if ($text -match 'm_SerializationMode:\s*(\d+)' -and $Matches[1] -ne '2') { $bad += 'Asset Serialization must be Force Text' }
    if ($text -match 'm_ExternalVersionControlSupport:\s*(.+)' -and $Matches[1].Trim() -notmatch 'Visible Meta Files') {
        $bad += 'Version Control must be Visible Meta Files'
    }
    if ($bad) { Fail 'R11' 'Unity serialization settings changed' $bad } else { Pass 'R11' 'Force Text + Visible Meta Files' }
}
else { Warn 'R11' 'EditorSettings.asset not found (project not scaffolded yet?)' @($editorSettings) }

# ------------------------------------------------------------ R14 GDD sync ----
$rulesTouched = $changed | Where-Object { $_ -like "$projPrefix`Assets/Scripts/Rules/*" }
$gddTouched = $changed | Where-Object { $_ -like '*docs/GDD.md' }
if ($rulesTouched -and -not $gddTouched) {
    Warn 'R14' 'Rules layer changed but docs/GDD.md was not updated - is a documented rule now different?' $rulesTouched
}
else { Pass 'R14' 'GDD sync' }

# -------------------------------------------------------- R15 version bump ----
$codeTouched = $changed | Where-Object { $_ -like "$projPrefix`Assets/*" }
$versionTouched = $changed | Where-Object { $_ -like '*ProjectSettings/ProjectSettings.asset' }
if ($codeTouched -and -not $versionTouched) {
    Warn 'R15' 'Feature changed but bundleVersion was not bumped' @('ProjectSettings/ProjectSettings.asset')
}
else { Pass 'R15' 'version bump' }

# ------------------------------------------------------------------ done ----
Write-Host ""
if ($failures -gt 0) {
    Write-Host "$failures rule violation(s), $warnings warning(s). Fix the failures before opening a PR." -ForegroundColor Red
    Write-Host "Rules: docs/COLLABORATION.md" -ForegroundColor Red
    exit 1
}
Write-Host "All rules passed ($warnings warning(s))." -ForegroundColor Green
exit 0
