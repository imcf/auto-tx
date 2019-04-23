# Helper script to facilitate the package-deploy-update cycle. Its purpose is to
# automate reading the updater config (optionally just using the default
# location) and subsequently call the `Make-Package.ps1` and
# `Provide-UpdaterPackage.ps1` scripts followed by a removal of the update
# marker file for the local computer, all using the parameters collected from
# the configuration file.

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

# Make sure to run from the directory containing the script itself:
$BaseDir = $(Split-Path $MyInvocation.MyCommand.Path)
Push-Location $BaseDir

.\Make-Package.ps1
.\Provide-UpdaterPackage.ps1 -UpdaterSettings $UpdaterSettings

$Marker = "$($UpdateSourcePath)\Service\UpdateMarkers\$($env:COMPUTERNAME)"
if (Test-Path $Marker) {
    Remove-Item -Force -Verbose $Marker
}

Pop-Location