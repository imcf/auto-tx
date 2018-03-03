# ![AutoTx logo][img_autotx_logo] AutoTx - AutoTransfer Service

AutoTx is a Windows service doing background file transfers from a local disk to
a network share, licensed under the [GPLv3](LICENSE), developed and provided by
the [Imaging Core Facility (IMCF)][web_imcf] at the [Biozentrum][web_bioz],
[University of Basel][web_unibas], Switzerland.

It was primarily designed and developed for getting user-data off microscope
acquisition computers **after acquisition** with these goals:

- The user owning the data should be able to log off from the computer after
  initiating the data transfer, enabling other users to log on while data is
  still being transferred.
- Any other interactive session at the computer must not be disturbed by the
  background data transfer (in particular any data acquisition done by the next
  user at the system).
- No additional software has to be operated by the user for initiating the
  transfer, avoiding the need for learning yet another tool.

## Features

- **User-initiated:** data is actively "handed over" to the service by dropping
  it into a specific folder, commonly referred to as the "incoming location".
- **Monitoring of system-critical parameters:** the service has a number of
  configurable system parameters that are constantly being monitored. If one of
  them is outside their defined valid range, any running transfer will be
  immediately suspended and no new transfers will be started.
- **Auto-Resume:** if a transfer is interrupted due to system limitations or the
  operating system being shut down the transfer gets automatically resumed as
  soon as possible without requiring any user interaction.
- **Email notifications:** the user is notified via email of completed
  transfers, as well as on transfer interruptions (system being shutdown or
  similar).
- **Error reporting:** in addition to user notifications, the service will send
  error notifications via email to a separate admin address.

## Concept

The service is expected to operate in an *ActiveDirectory* (AD) environment,
with a dedicated AD-account (referred to as the *service account*) being used to
run the service on the client computer(s). Furthermore, a network share is
expected (currently only a single one is supported) where the service account
has appropriate permissions to copy data to.

For any user that should be allowed to use the transfer service, a dedicated
folder has to exist on this network share, the name of the folder being the
(short) AD account name (i.e. the login name or *sAMAccountName*) of the user.

- AD-function-account
- remote share with username folders

## Under the hood

For the actual transfer task, the service is using a C# wrapper for the
Microsoft RoboCopy tool called [RoboSharp][web_robosharp].

## Requirements

- **ActiveDirectory integration:** service account, local r/w, remote w
- **.NET Framework:** version 4.5 required
- **Windows 7, 64 bit:** currently only this has been tested, 32 bit support is
  planned as well as support for newer Windows versions (Server 2012 is confimed
  as *not-working* at the moment).


## Installation

Currently the service doesn't have a *conventional* installer but rather has to
be registered using the `InstallUtil.exe` tool coming with the .NET framework. A
PowerShell script to help with the installation is provided with each AutoTx
package. To use the script, follow these steps:

- Log on to the computer using an account with adminstrative privileges.
- Edit the `ScriptsConfig.ps1` settings file, adjust the values according to
  your setup.
- Open a PowerShell console using the `Run as Administrator` option. This is
  absolutely crucial, as otherwise `InstallUtil` will fail to do its job. Simply
  being logged on to the computer as an admin is **NOT SUFFICIENT!** The script
  [Run-ElevatedPowerShell.ps1](AutoTx/Resources/Run-ElevatedPowerShell.ps1) can
  be used to start a shell with elevated permissions.
- Navigate to the installation package directory, run the `Install-Service.ps1`
  script.

### Manual Installation

For detailed steps on how to do the installation steps yourself, have a look at
the [manual installation](INSTALLATION-MANUAL.md) instructions.

## Operation

### Logging

The Windows Event Log seems to be a good place for logging if you have a proper
monitoring solution in place, which centrally collects and checks it. Since we
don't have one, and none of the other ActiveDirectory setups known to us have on
either, the service places its log messages in a plain text file in good old
Unix habits.

Everything that needs attention is written into the service's base directory in
a file called `service.log`. There is another PS1 script in the Resources
directory that is showing the content of the log file on the console in real-
time (like `tail -f` on Unix).

### Status

Same as for the log messages, the service stores its status in a file, just this
is in XML format so it is easily usable from C# code using the core
Serialization / Deserialization functions. Likewise, this file is to be found in
the service base directory and called `status.xml`.

### Grace Location Cleanup

After a transfer has completed, the service moves all folders of that transfer
into one subfolder inside the `$ManagedDirectory/DONE/<username>/` location. The
subfolders are named with a timestamp `YYYY-MM-DD__hh-mm-ss`. The grace location
checks are done
 - at service startup
 - after a transfer has finished
 - once every *configurable* hours


## Development

### Code Structure

The code has evolved from a very basic project, and is currently mostly a
monolithic structure where the major part of the functionality is contained in
one central class. This is certainly not the most elegant design, but it is
getting the job done. Refactoring suggestions and / or pull requests are
welcome!

### Prerequisites

- **VisualStudio 2013** - the *Community Edition* is sufficient. Newer versions
  of VisualStudio should work but have not been tested.
- **ReSharper** - JetBrains ReSharper Ultimate 2017.1 has been used for
  development. Respecting the *Coding Style* will be very difficult without it,
  not speaking of the other features you might miss. So basically this is a
  must...
- To build the C# bindings for RoboCopy, please don't use the code from the
  original repository on github at the moment (it contains all kinds of build-
  artifacts and has a few other glitches) but rather the fork provided here:
  [RoboSharp fork][web_robosharp_fork].


### Building + Installing

- **TODO**: explain how to build
- After building the service, use the
  [Make-Package.ps1](AutoTx/Resources/Make-Package.ps1) script to create an
  installation package. It will contain the previously mentioned
  [Install-Service.ps1](AutoTx/Resources/Install-Service.ps1) script, the latest
  configuration file version and of course the required service binaries, DLLs,
  etc.


[img_autotx_logo]: https://git.scicore.unibas.ch/vamp/auto-tx/raw/master/Resources/auto-tx-logo.png

[web_imcf]: https://www.biozentrum.unibas.ch/imcf
[web_bioz]: https://www.biozentrum.unibas.ch/
[web_unibas]: https://www.unibas.ch/
[web_robosharp]: https://github.com/tjscience/RoboSharp
[web_robosharp_fork]: https://git.scicore.unibas.ch/vamp/robosharp