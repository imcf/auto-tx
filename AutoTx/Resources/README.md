Service Installation Updates
============================

The service can automatically be updated by running the `Update-AutoTxService.ps1`
script. It will check a remote location (configured in the script header) and do
the following tasks:

- check for new service binaries and update the local ones if applicable
- check for a new configuration file for this host and update the local one
- try to restart the service if one of the previous tasks was done

Automatic Updates
-----------------

To automate the above, a *scheduled task* has to be created. This can easily be
done by using the following PowerShell commands:

```powershell
# create a repetition interval
$TimeSpan = New-TimeSpan -Minutes 1


# configure a JobTrigger for the task using the repetition interval from above, repeating forever
$JobTrigger = New-JobTrigger `
    -Once `
    -At (Get-Date).Date `
    -RepetitionInterval $TimeSpan `
    -RepeatIndefinitely


# configure the JobOptions for the task (battery options should not be required on a fixed system,
# but doesn't hurt either)
$JobOptions = New-ScheduledJobOption `
    -RunElevated `
    -StartIfOnBattery `
    -ContinueIfGoingOnBattery


# set credentials for running the task (requires permission to start/stop the service
# and overwriting the configuration and binaries)
$Cred = Get-Credential


# register the job for execution
Register-ScheduledJob `
    -FilePath C:\Tools\AutoTx\Update-AutoTxService.ps1 `
    -Name "Update-AutoTxService" `
    -ScheduledJobOption $JobOptions `
    -Trigger $JobTrigger `
    -Credential $Cred `
    -Verbose
```