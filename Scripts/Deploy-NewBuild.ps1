$UpdaterSettings = "C:\Tools\AutoTx-Updater\UpdaterConfig.inc.ps1"

. $UpdaterSettings

.\Make-Package.ps1
.\Provide-UpdaterPackage.ps1 -UpdaterSettings $UpdaterSettings

$Marker = "$($UpdateSourcePath)\Service\UpdateMarkers\$($env:COMPUTERNAME)"
if (Test-Path $Marker) {
    Remove-Item -Force -Verbose $Marker
}