$LocalConfiguration = ".\ScriptsConfig.ps1"
if (Test-Path $LocalConfiguration) {
	. $LocalConfiguration
} else {
	Write-Host "Can't find configuration '$LocalConfiguration'!" -Fore Red
	Exit
}
Write-Host "Loaded configuration '$LocalConfiguration'." -Fore Green
Write-Host $ServiceDir
Write-Host $SourceDir

Push-Location "C:\Windows\Microsoft.NET\Framework\v4.0.30319"

$ServiceExe = $ServiceDir + "\" + $ServiceName + ".exe"
.\InstallUtil.exe -u $ServiceExe

Pop-Location