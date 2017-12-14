Service Installation Updates
============================

The service can automatically be updated by running the `Update-Service.ps1`
script. It will check a remote location (configurable via a config file) and do
the following tasks:

- check for new service binaries and update the local ones if applicable
- check for a new configuration file for this host and update the local one
- try to restart the service if one of the previous tasks was done

Config File Options
-------------------

An example config file for the update script is provided as
`UpdaterConfig-Example.inc.ps1`. The values should be mostly self-explaining, so
just a few comments here:

- `$InstallationPath` refers to the local directory where the service
  executables have been installed, e.g. `C:\Tools\AutoTx`
- `$UpdateSourcePath` points to the base directory on a storage location (most
  likely some UNC path) where the service update files are provided. See the
  next section for details on the structure therein.

Folder Structure
----------------

The `$UpdateSourcePath` folder structure is expected to be like this:

```
├─── Configs
│   └─── <HOSTNAME>
│       └─── configuration.xml
└─── Service
    ├─── Binaries
    │   ├─── build_2018-01-21_17-18-19
    │   │   └─── AutoTx
    │   └─── build_2018-01-23_11-22-33
    │       └─── AutoTx
    └─── UpdateMarkers
        └─── <HOSTNAME>
```

Automatic Updates
-----------------

To automate the above, a *scheduled task* has to be created. This can easily be
done by using the following PowerShell commands:

```powershell
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
    -Name "Update-AutoTxService" `
    -FilePath C:\Tools\AutoTx-Updater\Update-Service.ps1 `
    -ArgumentList C:\Tools\AutoTx-Updater\UpdaterConfig.inc.ps1 `
    -ScheduledJobOption $JobOptions `
    -Trigger $JobTrigger `
    -Credential $Cred `
    -Verbose
```