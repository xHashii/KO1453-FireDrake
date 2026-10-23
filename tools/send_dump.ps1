<#
.SYNOPSIS
  Sends a large MSSQL .sql dump to the agent through git.

.DESCRIPTION
  Zips the dump (text SQL compresses extremely well). If the .zip is <= 95 MB
  it is committed as a single file; otherwise it is split into parts with
  tools/split_dump.py. The payload is pushed to a throwaway 'db-drop'
  branch - never to the rewrite branch. The repository is private; after the
  agent confirms conversion, delete the db-drop branch on GitHub.

  Run this from a clone of the repository, e.g.:
    .\tools\send_dump.ps1 -DumpPath "C:\KO\KO_MAIN_full.sql"
  To send BOTH databases, run the script once per file.

.PARAMETER DumpPath
  Path to the .sql file to send.

.PARAMETER Branch
  Throwaway branch to push to. Default: db-drop

.PARAMETER BaseBranch
  Branch the throwaway branch is based on (needs tools/). Default: the
  rewrite session branch.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$DumpPath,
    [string]$Branch = "db-drop",
    [string]$BaseBranch = "arena/a8daa4cb-ko1453-firedrake"
)

$ErrorActionPreference = "Stop"

function Fail([string]$msg) { Write-Host "[send_dump] ERROR: $msg" -ForegroundColor Red; exit 1 }

if (-not (Test-Path -LiteralPath $DumpPath)) { Fail "File not found: $DumpPath" }
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail "git is not on PATH. Install Git for Windows first." }

$repoRoot = (git rev-parse --show-toplevel)
if ($LASTEXITCODE -ne 0) { Fail "Not inside a git repository. Clone the repo first: git clone https://github.com/xHashii/KO1453-FireDrake.git" }

$dump = Get-Item -LiteralPath $DumpPath
$sizeMB = [math]::Round($dump.Length / 1MB, 1)
Write-Host "[send_dump] Dump: $($dump.FullName) ($sizeMB MB)"

# Resolve all git paths (especially `git add db-drop`) from the repository
# root even if the script was launched from a subdirectory. $dump.FullName
# was already resolved above, so changing location cannot break a relative
# -DumpPath supplied by the caller.
Set-Location -LiteralPath $repoRoot

# --- Get onto the throwaway branch (based on the rewrite branch) ----------
git fetch origin
if ($LASTEXITCODE -ne 0) { Fail "git fetch failed" }

git rev-parse --verify $Branch 1>$null 2>$null
if ($LASTEXITCODE -eq 0) {
    git checkout $Branch
    if ($LASTEXITCODE -ne 0) { Fail "git checkout $Branch failed" }
} else {
    git checkout -b $Branch "origin/$BaseBranch"
    if ($LASTEXITCODE -ne 0) { Fail "Could not create branch $Branch from origin/$BaseBranch" }
}

# --- Prepare payload folder ------------------------------------------------
$payloadDir = Join-Path $repoRoot "db-drop"
New-Item -ItemType Directory -Force -Path $payloadDir | Out-Null

$zipPath = Join-Path $payloadDir ($dump.BaseName + ".zip")
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }

Write-Host "[send_dump] Compressing (this can take a minute for ~100+ MB)..."
Compress-Archive -LiteralPath $dump.FullName -DestinationPath $zipPath -CompressionLevel Optimal
$zip = Get-Item -LiteralPath $zipPath
$zipMB = [math]::Round($zip.Length / 1MB, 1)
Write-Host "[send_dump] Zipped size: $zipMB MB"

if ($zip.Length -gt 95MB) {
    Write-Host "[send_dump] Zip is still over 95 MB - splitting into 40 MB parts..."
    $splitter = Join-Path $repoRoot "tools\split_dump.py"
    if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
        Fail "python is not on PATH, needed to split. Install Python 3 (python.org) or split the zip with 7-Zip volumes instead."
    }
    $zipBackup = Join-Path ([System.IO.Path]::GetTempPath()) $zip.Name
    Copy-Item $zipPath $zipBackup -Force
    Remove-Item $zipPath -Force
    python $splitter split $zipBackup $payloadDir --part-size-mb 40
    if ($LASTEXITCODE -ne 0) { Fail "split_dump.py failed" }
    Remove-Item $zipBackup -Force
    Write-Host "[send_dump] Split done."
} else {
    Write-Host "[send_dump] Fits in a single git push - no splitting needed."
}

# --- Commit and push --------------------------------------------------------
git add -f db-drop
git commit -m "DB dump drop: $($dump.Name) (temporary, do not merge)"
if ($LASTEXITCODE -ne 0) { Fail "git commit failed (nothing to commit?)" }

Write-Host "[send_dump] Pushing to origin/$Branch ..."
git push -u origin $Branch
if ($LASTEXITCODE -ne 0) { Fail "git push failed" }

Write-Host ""
Write-Host "[send_dump] DONE. Tell the agent: 'Dump pushed to branch $Branch'." -ForegroundColor Green
Write-Host "[send_dump] To send the second database, run this script again with its .sql file."
Write-Host "[send_dump] AFTER the agent confirms the conversion, delete the branch:"
Write-Host "            git push origin --delete $Branch"
