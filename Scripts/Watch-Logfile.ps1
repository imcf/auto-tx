# Make sure to run from the directory containing the script itself:
$BaseDir = $(Split-Path $MyInvocation.MyCommand.Path)
Push-Location $BaseDir

$LocalConfiguration = ".\ScriptsConfig.ps1"
if (Test-Path $LocalConfiguration) {
	. $LocalConfiguration
} else {
	Write-Host "Can't find configuration '$LocalConfiguration'!" -Fore Red
	Exit
}

$LogFile = "$($ServiceDir)\var\$($env:COMPUTERNAME).$($ServiceName).log"

if (Test-Path $LogFile) {
	Write-Host "Watching logfile '$LogFile':"
	Get-Content -Tail 200 -Wait $LogFile
} else {
	Write-Host "Logfile '$LogFile' doesn't exist."
}

Pop-Location