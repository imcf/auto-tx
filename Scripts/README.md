Build-Package-Deploy Cycle
==========================

NOTE: everything here assumes default installation locations!

Run the `Deploy-NewBuild.ps1` script to package a new build, place the files at
their appropriate location and remove the update marker file for this host. Then
use a shell with the appropriate permissions and run the updater script using
the following command (or trigger it through the scheduled jobs if installed):

```powershell
C:\Tools\AutoTx-Updater\Update-Service.ps1 -UpdaterSettings C:\Tools\AutoTx-Updater\UpdaterConfig.inc.ps1
```
