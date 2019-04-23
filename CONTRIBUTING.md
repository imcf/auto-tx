# AutoTx Development And Contribution Guide

## Code Structure

The code has evolved from a very basic project, initially all being a
monolithic structure where the major part of the functionality is contained in
one central class. This is certainly not the most elegant design, but it was
getting the job done. A lot of refactoring has been done with the project
currently being split into a few separate parts:

- [ATxCommon](ATxCommon): all the common components like configuration and
  status serialization wrappers, unit converters, file system tasks and so on
  bundled as a runtime library.
- [ATxService](ATxService): the core service functionality.
- [ATxConfigTest](ATxConfigTest): a small command line tool allowing to validate
  a given configuration file and show a summary. Used in the updater to ensure
  new configurations are valid before overwriting existing ones.
- [ATxTray](ATxTray): the tray application.
- [ATxDiagnostics](ATxDiagnostics): a command line tool to run a few tests and
  report the results.
- [Updater](Updater): the updater script for binaries and configuration files.

Refactoring suggestions and / or pull requests are welcome!

## Prerequisites

- **VisualStudio 2017** - the *Community Edition* is sufficient.
- **ReSharper** - JetBrains ReSharper Ultimate 2017.1 has been used for
  development. Respecting the *Coding Style* will be very difficult without it,
  not speaking of the other features you might miss. So basically this is a
  must...
- For having the assembly details automatically filled with version details at
  build-time, a working installation of **Git** and **PowerShell** is required.
- To build the C# bindings for RoboCopy, it is now fine to use the sources from
  the [original repository on github][web_robosharp]. To benefit from
  automatically filling assembly details during the build you can use our fork
  provided here: [RoboSharp fork][web_robosharp_fork].


## Building + Installing

- Open the solution file in *Visual Studio* and adjust the path to the
  *RoboSharp* DLL under *Solution Explorer* > *ATxService* > *References*. Then
  simply build all components by pressing *F6* or by selecting *Build Solution*
  from the *Build* menu.
- After building the service, use the
  [Make-Package.ps1](AutoTx/Resources/Make-Package.ps1) script to create an
  installation package. It will contain the previously mentioned
  [Install-Service.ps1](AutoTx/Resources/Install-Service.ps1) script, the latest
  configuration file version and of course the required service binaries, DLLs,
  etc.

## Making Changes

- Do [atomic commits][web_commit].
- For any change that consists of more than a single commit, create a topic
  branch.
- Check for unnecessary whitespace with `git diff --check` before committing.
- Make sure your commit messages are in a proper format.
  - Use the present tense ("Add feature" not "Added feature").
  - Use the imperative mood ("Change foo to..." not "Changes foo to...").
  - Limit the line length to 80 characters or less (72 for the first line).
  - Have the second line be empty.
  - If in doubt about the format, read [Tim Pope's note about git commit
    messages][web_tbaggery].
  - If the commit addresses an issue filed **on GitHub** please **DO NOT**
    reference that issue in the commit message (issue tracking is done in the
    [primary repository in the Uni Basel GitLab][web_autotx_gitlab]).

## The Full Cycle: Change / Compile / Package / Update / Test

To run a typical development cycle, a few helper scripts are provided, most
notably to facilitate / automate that cycle whilst using the common
functionalities coming with AutoTx (like the updater).

### Building / Compiling

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

### Creating an Installation / Updater Package

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

### Triggering the Updater

Running the updater can be done in various ways, for example you could simply
call the script manually from a PowerShell session with sufficient privileges.
However, the recommended method is to actually trigger the scheduled task to
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

## Coding Conventions

As mentioned above, the **C#** style used throughout the project differs from
the default one suggested by Microsoft, mostly in being much more compact when
using curly brackets - a little bit inspired by the common Python coding
conventions. The formatting rules are stored in the project's *ReSharper*
settings, so simply use that to do the formatting job.

For the **PowerShell** parts there is one major difference about the naming of
(internal) functions, disregarding the common `Verb-Noun` convention. In places
where it improves readability of the code, functions may be named different.
Compare e.g.

```powershell
if (ServiceIsRunning $ServiceName) { Write-Host "Success" }
```
vs.

```powershell
if (Check-Service $ServiceName) { Write-Host "Success" }
```
where the former one is literally readable and concise, whereas the latter one
requires those not familiar with the code to go and check what the method
actually does.

[web_robosharp]: https://github.com/tjscience/RoboSharp
[web_robosharp_fork]: https://git.scicore.unibas.ch/vamp/robosharp
[web_commit]: https://en.wikipedia.org/wiki/Atomic_commit#Atomic_commit_convention
[web_tbaggery]: https://tbaggery.com/2008/04/19/a-note-about-git-commit-messages.html
[web_autotx_gitlab]: https://git.scicore.unibas.ch/vamp/auto-tx