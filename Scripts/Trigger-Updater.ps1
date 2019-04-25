# Helper script to trigger the updater if the corresponding marker file has
# been removed (e.g. by using the `Deploy-NewBuild.ps1` script).

#Requires -RunAsAdministrator

[CmdletBinding()]
Param(
    [String] $UpdaterSettings = "C:\Tools\AutoTx-Updater\UpdaterConfig.inc.ps1"
)

$ErrorActionPreference = "Stop"

try {
    . $UpdaterSettings
}
catch {
    $ex = $_.Exception.Message
    Write-Host "Error reading settings file: '$($UpdaterSettings)' [$($ex)]"
    Exit
}

$Marker = "$($UpdateSourcePath)\Service\UpdateMarkers\$($env:COMPUTERNAME)"
Write-Host "Using marker file $Marker"

while ($true) {
    if (Test-Path $Marker) {
        # Write-Host "marker file found"
    } else {
        # Write-Host "NO marker file found, starting updater..."
        (Get-ScheduledJob -Name "AutoTx-Updater").StartJob()
        # Allow the updater to complete its run:
        Start-Sleep 30
    }
    Start-Sleep 1
}