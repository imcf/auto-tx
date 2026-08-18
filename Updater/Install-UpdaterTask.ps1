$InstallDir = "C:\Tools\AutoTx-Updater"
$JobScript = "Update-Service.ps1"
$Config = "UpdaterConfig.inc.ps1"

if (Test-Path $InstallDir) {
    Write-Host "ERROR: updater directory already existing, stopping installer!"
    Write-Host "[$($InstallDir)]"
    Exit
}
if (-Not (Test-Path $Config)) {
    Write-Host "ERROR: no config file for the updater found!"
    Write-Host "[$($Config)]"
    Exit
}
New-Item -Force -Type Directory $InstallDir
Copy-Item $JobScript $InstallDir
Copy-Item $Config $InstallDir


# create a repetition interval
$TimeSpan = New-TimeSpan -Minutes 10


# configure a JobTrigger for the task using the repetition interval from above,
# repeating forever
$JobTrigger = New-JobTrigger `
    -Once `
    -At (Get-Date).Date `
    -RepetitionInterval $TimeSpan `
    -RepeatIndefinitely


# configure the JobOptions for the task (battery options should not be required
# on a fixed system, but doesn't hurt either)
$JobOptions = New-ScheduledJobOption `
    -RunElevated `
    -StartIfOnBattery `
    -ContinueIfGoingOnBattery


# set credentials for running the task (requires permission to start/stop the
# service and overwriting the configuration and binaries)
$Cred = Get-Credential

# register the job for execution
Register-ScheduledJob `
    -Name "AutoTx-Updater" `
    -FilePath "$($InstallDir)\$($JobScript)" `
    -ArgumentList "$($InstallDir)\$($Config)" `
    -ScheduledJobOption $JobOptions `
    -Trigger $JobTrigger `
    -Credential $Cred `
    -Verbose