<#
.SYNOPSIS
    Fetches PresentMon into the shared Raisin tool cache, at a pinned version.

.DESCRIPTION
    PresentMon is how any of these apps finds out what the display actually showed, as opposed to
    what the app asked for. Frames that miss a composition deadline are picked up at a later vblank,
    and from inside the process that is invisible — an in-process log can only say a repaint was
    requested and served.

    A pinned version downloaded on demand rather than a binary in the repository: it is a megabyte
    of someone else's build, it is MIT, and a fresh clone can reproduce a capture without carrying
    it in history.

    The version is pinned because captures get compared across weeks. PresentMon renames columns
    between major versions — 1.x has `Dropped` and `msBetweenPresents`, 2.x has `DisplayedTime` and
    `MsAnimationError` — so an unpinned tool would silently change what a harness is reading.

.PARAMETER Version
    PresentMon release to fetch. 2.x is wanted for MsAnimationError, which is the difference between
    a frame's CPU delta and its display delta — the metric that describes what an eye actually sees.
    1.x does not have it.

.PARAMETER Force
    Re-download even if the cached copy is already there.

.EXAMPLE
    .\get-presentmon.ps1
    Ensures the tool is present and prints its path.
#>
[CmdletBinding()]
param(
    [string] $Version = '2.5.1',
    [string] $CacheDir = "$env:LOCALAPPDATA\Raisin\tools",
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

$exe = Join-Path $CacheDir "PresentMon-$Version.exe"

if ((Test-Path $exe) -and -not $Force) {
    Write-Host "already present: $exe"
    return $exe
}

New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null
$url = "https://github.com/GameTechDev/PresentMon/releases/download/v$Version/PresentMon-$Version-x64.exe"

Write-Host "downloading PresentMon $Version"
try {
    Invoke-WebRequest -Uri $url -OutFile $exe -UseBasicParsing
} catch {
    throw "could not download PresentMon $Version from $url : $($_.Exception.Message)"
}

# A truncated download is a working file that captures nothing, so check it looks like the ~1MB
# standalone build rather than an error page saved with the right name.
$size = (Get-Item $exe).Length
if ($size -lt 500KB) {
    Remove-Item $exe -Force
    throw "download was only $size bytes - that is not the standalone build. Check the version exists."
}

Write-Host ("fetched {0:N0} bytes -> {1}" -f $size, $exe)

# Capturing needs ETW access: Administrator, or membership of Performance Log Users. Without it
# PresentMon starts, records nothing, and exits successfully — which looks exactly like a capture
# of an idle application.
$id = [Security.Principal.WindowsIdentity]::GetCurrent()
$canCapture = ([Security.Principal.WindowsPrincipal]$id).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $canCapture) {
    foreach ($g in $id.Groups) {
        try { if ($g.Translate([Security.Principal.NTAccount]).Value -match 'Performance Log Users') { $canCapture = $true; break } } catch { }
    }
}
if (-not $canCapture) {
    Write-Warning "Not elevated and not in 'Performance Log Users' - captures will come back empty."
}

return $exe
