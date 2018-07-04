Service Updates, Configuration Management and Logfiles Collection
=================================================================

The service can automatically be updated by running the `Update-Service.ps1`
script. It will check a remote location (configurable via a config file) and do
the following tasks:

- check if new service binaries should be installed on the system
- check if new configuration files are available for the given machine
- validate if configuration and service are compatible (in all possible
  combinations, i.e. new binaries with existing config, new config with existing
  binaries and finally new binaries with new config files)
- update those components that were detected to require updating before
- restart the service if applicable
- report to the Windows Event Log as well as via email
- upload the local service log file to the storage location that is also used
  to retrieve updates

Updater Config File Options
---------------------------

An example config file for the update script is provided as
`UpdaterConfig-Example.inc.ps1`. The values should be mostly self-explaining, so
just a few comments here:

- `$InstallationPath` refers to the local directory where the service
  executables have been installed, e.g. `C:\Tools\AutoTx`
- `$UpdateSourcePath` points to the base directory on a storage location (most
  likely some UNC path) where the service update files are provided. See the
  next section for details on the structure therein.
- `$Pattern` is a regular expression that will be used to locate possible
  update packages in the given path matching this expression.

Folder Structure
----------------

The `$UpdateSourcePath` folder structure is expected to be like this:

```
├─── Configs
│   ├─── config.common.xml
│   ├─── <HOSTNAME1>.xml
│   └─── <HOSTNAME2>.xml
├─── Logs
│   ├─── <HOSTNAME1>.AutoTx.log
│   └─── <HOSTNAME2>.AutoTx.log
└─── Service
    ├─── Binaries
    │   ├─── build_2018-01-21_17-18-19
    │   │   └─── AutoTx
    │   └─── build_2018-01-23_11-22-33
    │       └─── AutoTx
    └─── UpdateMarkers
        ├─── <HOSTNAME1>
        └─── <HOSTNAME2>
```

Permissions
-----------

The updater script needs to be run with an account that has applicable
permissions to start and stop the service, has write permissions to the local
service installation directory and the `$UpdateSourcePath` location (the latter
one only requires write-permissions for the `Logs` folder for uploading the log
files).

Automatic Updates
-----------------

To automate the above, a *scheduled task* has to be created. This can easily be
done by using the following PowerShell commands (or by running the provided
`Install-UpdaterTask.ps1` script):

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
