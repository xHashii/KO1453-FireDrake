param(
    [Parameter(Mandatory = $true)][string]$ClientRoot,
    [string]$OutputPath = "ko-client-info.json"
)
# Read-only diagnostics. Does not launch/patch the client, alter drivers/registry,
# or collect configuration contents, account names, process dumps or credentials.
$ErrorActionPreference = "Stop"
$root = (Resolve-Path -LiteralPath $ClientRoot).Path
if (-not (Test-Path -LiteralPath $root -PathType Container)) {
    throw "ClientRoot must point to the game client folder, not the server repository."
}
function Describe-File($file) {
    [ordered]@{
        Name = $file.FullName.Substring($root.Length).TrimStart('\', '/')
        Bytes = $file.Length
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        FileVersion = $file.VersionInfo.FileVersion
        ProductVersion = $file.VersionInfo.ProductVersion
    }
}
$executables = @(Get-ChildItem -LiteralPath $root -File -Filter "*.exe" |
    Where-Object { $_.Name -match 'knight|ko(client)?\.exe' } |
    ForEach-Object { Describe-File $_ })
if ($executables.Count -eq 0) {
    throw "No Knight Online client executable found at ClientRoot. Select the folder containing KnightOnLine.exe."
}
$assets = @()
foreach ($folder in @('Data', 'UI', 'UI_US')) {
    $path = Join-Path $root $folder
    if (Test-Path -LiteralPath $path -PathType Container) {
        $assets += @(Get-ChildItem -LiteralPath $path -File -Recurse |
            Where-Object { $_.Name -match '^item.*\.tbl$|tooltip|item.*info.*\.uif$' } |
            Sort-Object FullName | Select-Object -First 100 |
            ForEach-Object { Describe-File $_ })
    }
}
$warnings = @()
$os = $null
$gpus = @()
try {
    $system = Get-CimInstance Win32_OperatingSystem
    $os = @{ Caption = $system.Caption; Version = $system.Version; Build = $system.BuildNumber }
    $gpus = @(Get-CimInstance Win32_VideoController |
        Select-Object Name, DriverVersion, DriverDate)
} catch { $warnings += "Windows/GPU information unavailable: $($_.Exception.Message)" }
$fontNames = @()
try {
    Add-Type -AssemblyName System.Drawing
    $fonts = New-Object System.Drawing.Text.InstalledFontCollection
    try {
        $fontNames = @($fonts.Families | Where-Object { $_.Name -match 'Arial|Gulim|Tahoma|Segoe|Malgun' } |
            Select-Object -ExpandProperty Name | Sort-Object -Unique)
    } finally { $fonts.Dispose() }
} catch { $warnings += "Font enumeration unavailable: $($_.Exception.Message)" }
[ordered]@{
    Purpose = "Read-only KO item-tooltip diagnostics; no performance conclusion inferred"
    OS = $os
    GPUs = $gpus
    RelevantFonts = $fontNames
    Executables = $executables
    ItemAndTooltipAssets = $assets
    Warnings = $warnings
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Saved $OutputPath. Review it before sharing. No client or Windows settings were changed."
