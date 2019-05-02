# Build-Package-Deploy Cycle

Instructions on running a typical development cycle using the helper /
convenience scripts provided here to automate the steps as much as possible.

## Building / Compiling

Open a shell session using the `Developer Command Prompt for VS 2017` shortcut
(doesn't require any special permissions, i.e. can be your user that's also
running Visual Studio). For convenience reasons it is recommended to start
`PowerShell` within this terminal, but this is not strictly required. Navigate
to the `AutoTx` source code base directory, then call any of the `.cmd` scripts
under `Scripts/msbuild` depending on your needs. For a *Debug* build this would
be something like this:

```PowerShell
cd  C:\Devel\AutoTx   ## <- adjust path to your requirements
.\Scripts\msbuild\build\debug.cmd
```

## Creating an Installation / Updater Package

After building the service has completed, use the `Deploy-NewBuild.ps1` script,
optionally with specifying the path to the updater configuration (by default
the script will assume "`C:\Tools\AutoTx-Updater\UpdaterConfig.inc.ps1`" for
this).

```PowerShell
.\Scripts\Deploy-NewBuild.ps1
# or by specifying the config explicitly:
.\Scripts\Deploy-NewBuild.ps1 -UpdaterSettings C:\Path\To\UpdaterConfig.inc.ps1
```

Running this should produce output similar to the one shown here:

```
Creating package [build_2019-04-23_14-14-42__3.0-70-gf90a55c] using binaries from:
    [C:\Devel\AutoTx\ATxService\bin\Debug]
    [C:\Devel\AutoTx\ATxTray\bin\Debug]
    [C:\Devel\AutoTx\ATxConfigTest\bin\Debug]
    [C:\Devel\AutoTx\ATxDiagnostics\bin\Debug]

Removing existing package dir [build_2019-04-23_14-14-42__3.0-70-gf90a55c]...

Done creating package [build_2019-04-23_14-14-42__3.0-70-gf90a55c]
    [configuration: Debug]
    [commit: 3.0-70-gf90a55c]

Location: [C:\Devel\AutoTx\Scripts\build_2019-04-23_14-14-42__3.0-70-gf90a55c]
```

## Combining Building and Packaging

To combine the two previous steps in one go, use this command:

```PowerShell
.\Scripts\msbuild\build\debug.cmd ; if ($?) { .\Scripts\Deploy-NewBuild.ps1 ; }
```

## Triggering the Updater

Running the updater can be done in various ways, for example you could simply
call the script manually from a PowerShell session with sufficient privileges
(*NOT recommended though*):

```PowerShell
C:\Tools\AutoTx-Updater\Update-Service.ps1 -UpdaterSettings C:\Tools\AutoTx-Updater\UpdaterConfig.inc.ps1
```

However, the way better approach is to actually trigger the scheduled task to
mimick a "*real-life*" run as closely as possible. This is doable using the
GUI (e.g. by launching `compmgmt.msc` and clicking all your way through the
"Task Scheduler" tree jungle). A much easier approach is to use the command line
again to run the job. Unfortunately there is no *straightforward* way to launch
a registered job on Windows 7 (on Windows 10 there is the `Start-ScheduledTask`
cmdlet), so you have to take a little detour. First of all make sure to have a
PowerShell session running with *elevated privileges* (i.e. it is **not enough**
to run it with an administrator account, you explicitly need to start it with
the "Run as Administrator" option), then issue this command:

```PowerShell
(Get-ScheduledJob -Name "AutoTx-Updater").StartJob()
```

This should trigger a run of the updater, any actions will be recorded to the
Windows Event Log. In case the service was running before, it will be restarted
and show its related messages in the service log file.