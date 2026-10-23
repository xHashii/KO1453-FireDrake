<#
.SYNOPSIS
  Sends content archives (map / quest packs) to the agent through a throwaway
  branch, in one run, without touching your working checkout.

.DESCRIPTION
  The archives are copied AS-IS (they are already compressed - no re-zipping,
  and at these sizes no splitting) into a content-drop/ folder on a throwaway
  branch together with a MANIFEST.txt (name, byte size, SHA-256). The branch
  plays the role of a dummy repo: it is never merged, and you delete it on
  GitHub once the agent confirms it has taken the contents.

  Unlike tools/send_dump.ps1 this script never checks anything out in your
  clone: the drop commit is built in a temporary `git worktree` under your
  TEMP dir and removed afterwards, so repeated runs accumulate on the drop
  branch while you stay on your working branch the whole time.

  Run from a clone of the repository, e.g.:
    .\tools\send_content.ps1 -Paths "C:\ko\MAP.7z", "C:\ko\Maps & Quests.7z", "C:\ko\QUESTS.7z"

.PARAMETER Paths
  Archive files to send. Any container format (.7z, .zip, .rar...) - the
  script does not look inside, it only transports.

.PARAMETER Branch
  Throwaway branch to push to. Default: content-drop

.PARAMETER BaseBranch
  Starting point for the FIRST drop (when the drop branch does not exist yet).
  Default: the current rewrite session branch.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string[]]$Paths,
    [string]$Branch = "content-drop",
    [string]$BaseBranch = "arena/306b65a0-ko1453-firedrake"
)

$ErrorActionPreference = "Stop"

function Fail([string]$msg) { Write-Host "[send_content] ERROR: $msg" -ForegroundColor Red; exit 1 }

foreach ($p in $Paths) {
    if (-not (Test-Path -LiteralPath $p)) { Fail "File not found: $p" }
}

try { $repoRoot = (git rev-parse --show-toplevel) } catch { $repoRoot = $null }
if (-not $repoRoot) { Fail "Not inside a git repository. Clone the repo first." }
Set-Location -LiteralPath $repoRoot

git fetch origin
if ($LASTEXITCODE -ne 0) { Fail "git fetch failed" }

# Accumulate: continue an existing drop branch, otherwise start from the base.
$start = "origin/$BaseBranch"
if (git branch -r --list "origin/$Branch") { $start = "origin/$Branch" }

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ("content-drop-" + [guid]::NewGuid().ToString("n"))
git worktree prune
git worktree add --detach $tmp $start
if ($LASTEXITCODE -ne 0) { Fail "git worktree add failed" }

try {
    $payload = Join-Path $tmp "content-drop"
    New-Item -ItemType Directory -Force -Path $payload | Out-Null

    $manifest = @()
    foreach ($p in $Paths) {
        $item = Get-Item -LiteralPath $p
        if ($item.Length -gt 95MB) {
            Fail "$($item.Name) is over 95 MB - GitHub rejects it; split it first (tools/split_dump.py)."
        }
        $dest = Join-Path $payload $item.Name
        if (Test-Path -LiteralPath $dest) { Fail "Name already on the drop branch: $($item.Name)" }
        Copy-Item -LiteralPath $item.FullName -Destination $dest
        $hash = (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
        $manifest += "$($item.Name)`t$($item.Length)`t$hash"
        Write-Host "[send_content] staged $($item.Name) ($([math]::Round($item.Length / 1MB, 1)) MB)"
    }
    Set-Content -LiteralPath (Join-Path $payload "MANIFEST.txt") -Value $manifest -Encoding UTF8

    git -C $tmp add -f content-drop
    git -C $tmp commit -m "content drop: $($Paths.Count) archive(s) (temporary, do not merge)"
    if ($LASTEXITCODE -ne 0) { Fail "git commit failed (nothing to commit?)" }

    Write-Host "[send_content] Pushing to origin/$Branch ..."
    git -C $tmp push origin ("HEAD:refs/heads/" + $Branch)
    if ($LASTEXITCODE -ne 0) { Fail "push failed" }
}
finally {
    git worktree remove --force $tmp
    git worktree prune
}

Write-Host "[send_content] Done - origin/$Branch now carries the archives. Tell the agent the drop is complete." -ForegroundColor Green
