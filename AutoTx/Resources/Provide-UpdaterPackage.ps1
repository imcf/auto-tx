[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)][String] $UpdaterSettings
)

try {
    . $UpdaterSettings
}
catch {
    $ex = $_.Exception.Message
    Write-Host "Error reading settings file: '$($UpdaterSettings)' [$($ex)]"
    Exit
}


$UpdateBinariesPath = "$($UpdateSourcePath)\Service\Binaries"

$PackageDir = Get-ChildItem -Directory -Name |
    Where-Object {$_ -match $Pattern} |
    Sort-Object |
    Select-Object -Last 1

if ([string]::IsNullOrEmpty($PackageDir)) {
    Write-Host "ERROR: couldn't find any directories matching '$($Pattern)'!"
    Exit
}


try {
    # exclude some files not be distributed:
    $Exclude = @("ScriptsConfig.ps1", "Install-Service.ps1")
    Copy-Item -Recurse -Force -ErrorAction Stop `
        -Path $PackageDir `
        -Exclude $Exclude `
        -Destination $UpdateBinariesPath
    Write-Host "Copied package [$($PackageDir)] to [$($UpdateBinariesPath)]."
}
catch {
    $ex = $_.Exception.Message
    Write-Host "Error copying service package: $($ex)"
    Exit
}

