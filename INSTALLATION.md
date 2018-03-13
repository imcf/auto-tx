# AutoTx Service Installation

The AutoTx service doesn't have a *conventional* installer but rather has to be
registered using the `InstallUtil.exe` tool coming with the .NET framework.

## Using The Installation Script

A PowerShell script to help with the installation is provided with each AutoTx
package. To use the script, follow these steps:

- Log on to the computer using an account with adminstrative privileges.
- Edit the `ScriptsConfig.ps1` settings file, adjust the values according to
  your setup.
- Open a PowerShell console using the `Run as Administrator` option. The script
  [Run-ElevatedPowerShell.ps1](Scripts/Run-ElevatedPowerShell.ps1) can  be used
  to start a shell with elevated permissions.
- Navigate to the installation package directory, run the `Install-Service.ps1`
  script.
- Supply the two required configuration files in the `conf/` subdirectory of
  the service installation location and start the service using `Start-Service
  AutoTx`.


## Manual Installation Instructions

**PLEASE NOTE:** these instructions are mostly for documentation purposes, by
default it is **strongly recommended** to use the installation script described
above.

### Register the service

Open a *PowerShell* console with elevated privileges ("Run as Administrator").
Launch `InstallUtil` as follows:

```
& "C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe" /username=<SERVICEACCOUNT> /password=<SERVICEPASS> /unattended C:\Tools\AutoTx\AutoTx.exe
```

### Add the service account to group "Performance Monitor Users"

Monitoring the CPU load requires the service account to be a member of this
group. If this is not done via ActiveDirectory GPO's, you can do it for the
local system by running this command:

```
Add-LocalGroupMember -Group "Performance Monitor Users" -Member <SERVICEACCOUNT>
```

### Service Startup

```
Start-Service AutoTx
Get-Content -Wait -Tail 200 C:\Tools\AutoTx\service.log
```