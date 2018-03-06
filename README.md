# ![AutoTx logo][img_autotx_logo] AutoTx - AutoTransfer Service

AutoTx is a Windows service doing background file transfers from a local disk to
a network share, licensed under the [GPLv3](LICENSE), developed and provided by
the [Imaging Core Facility (IMCF)][web_imcf] at the [Biozentrum][web_bioz],
[University of Basel][web_unibas], Switzerland.

It is primarily designed and developed for getting user-data off microscope
acquisition computers **after acquisition** (i.e. not in parallel!) with these
goals:

- The user owning the data should be able to log off from the computer after
  initiating the data transfer, enabling other users to log on while data is
  still being transferred.
- Any other interactive session at the computer must not be disturbed by the
  background data transfer (in particular any data acquisition done by the next
  user at the system).
- No additional software has to be operated by the user for initiating the
  transfer, avoiding the need for learning yet another tool.

## Features

- **User-initiated:** data is actively "handed over" to the service by the user
  to prevent interfering with running acquisitions.
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
  error notifications via email to a separate admin address. Optionally, the
  service offers the possibility to monitor free disk space on the local disks
  and send notifications to the admins as well. Various measures are implemented
  to prevent the service from flooding you with emails.
- **Tray Application:** complementary to the service an application running in
  the system tray is provided, showing details on what's going on to the user.
- **Headless and GUI:** submitting a folder for transfer can either be done by
  dropping it into a specific "*incoming*" folder (using the File Explorer or
  some post-acquisition script or whatever fits your scenario) or by using the
  guided folder selection dialog provided through the tray app context menu.

## Concept

The service is expected to operate in an *ActiveDirectory* (AD) environment,
with a dedicated AD-account (referred to as the *service account*) being used to
run the service on the client computer(s). Furthermore, a network share is
expected (currently only a single one is supported) where the service account
has appropriate permissions to copy data to.

For any user that should be allowed to use the transfer service, a dedicated
folder has to exist on this network share, the name of the folder being the
(short) AD account name (i.e. the login name or *sAMAccountName*) of the user.

After the user initiates a transfer (i.e. hands over a folder to the AutoTx
service), the folder gets **immediately** moved to the *spooling* location on
the same disk. This is done to prevent users from accidentially messing with
folders subject to being transferred as well as for internal bookkeeping of what
has to be transferred.

When no other transfer is running and all system parameters are within their
valid ranges, the AutoTx service will start copying the files and folders to a
temporary transfer directory inside the target location. Only when a transfer
has completed, it will be moved from the temporary location over to the user's
folder. This has the benefit that a user can't accidentially access data from
incomplete transfers as well as it serves as a kind of implicit notification: if
a folder shows up in their location, the user will know it has been fully
transferred.

Once the transfer is completed the folder is moved from the local *spooling*
directory to a "*grace*" location inside the spooling directory hierarchy. This
is done to prevent accidentially deleting user data. Currently no automatic
deletion of data is implemented. Instead, the service keeps track of the grace
location and will send notification emails to the admin once a given time
period has expired (defaulting to 30 days).

## Under the hood

For the actual transfer task, the service is using a C# wrapper for the
Microsoft RoboCopy tool called [RoboSharp][web_robosharp].

Logging is done using the amazing [NLog][web_nlog] framework, allowing a great
deal of flexibility in terms of log levels, targets (file, email, eventlog) and
rules.

## Requirements

- **ActiveDirectory integration:** no authentication mechanisms for the target
  storage are currently supported, meaning the function account running the
  service on the client has to have local read-write permissions as well as full
  write permissions on the target location. The reason behind this is to avoid
  having to administer local accounts on all clients as well as having easy
  access to user information (email addresses, ...).
- **.NET Framework:** version 4.5 required.
- **Windows 7 / Server 2012 R2:** the service has been tested on those versions
  of Windows, other versions sharing the same kernels (*Server 2008 R2*,
  *Windows 8.1*) should be compatible as well but have yet been tested.
- **64 bit:** currently only 64-bit versions are available (mostly due to lack
  of options for testing), 32-bit support is planned though.


## Installation

The AutoTx service doesn't have a *conventional* installer but rather has to be
registered using the `InstallUtil.exe` tool coming with the .NET framework. A
PowerShell script to help with the installation is provided with each AutoTx
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

### Manual Installation

For detailed steps on how to do the installation steps yourself, have a look at
the [manual installation](INSTALLATION-MANUAL.md) instructions.

## Operation

### Configuration

The AutoTx service configuration is done through two XML files. They are
structured in a very simple way and well-commented to make them easily
readable. The first file `config.common.xml` defines settings which are common
to all AutoTx installations in the same network. The second file contains the
host-specific settings for the service and is using the machine's hostname for
its file name (followed by the `.xml` suffix). Both files are located in the
`conf/` folder inside the service installation directory and share the exact
same syntax with the host-specific file having priority (i.e. all settings
defined in the common file can be overridden in the host-specific one).

Having the configuration in this *layered* way allows an administrator to have
the exact same `conf/` folder on all hosts where AutoTx is installed, thus
greatly simplifying automated management.

Example config files (fully commented) are provided with the source code:

- [A minimal set](Resources/conf-minimal/) of configuration settings required
  to run the service.
- [The full set](Resources/conf/) of all possible configuration settings.

### Email-Templates

Notification emails to users are based on the templates that can be found in
[Mail-Templates](Resources/Mail-Templates) subdirectory of the service
installation. Those files contain certain keywords that will be replaced with
current values by the service before sending the mail. This way the content of
the notifications can easily be adjusted without having to re-compile the
service.


### Logging

The Windows Event Log seems to be a good place for logging if you have a proper
monitoring solution in place, which centrally collects and checks it. Since we
don't have one, and none of the other ActiveDirectory setups known to us have on
either, the service places its log messages in a plain text file in good old
Unix habits.

Everything that needs attention is written into the service's base directory in
a file called `AutoTx.log`. The contents of the log file can be monitored in
real-time using the PowerShell command `Get-Content -Wait -Tail 100 AutoTx.log`
or by running the [Watch-Logfile.ps1](Scripts/Watch-Logfile.ps1) script.

The log level can be set through the configuration file.

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
 - once every *N* hours, configurable for every host

### Updates

The service comes with a dedicated updater to facilitate managing updates and
configurations on many machines. See the [Updater Readme](Updater/README.md) for
all the details.


## Development

### Code Structure

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
- [Updater](Updater): the updater script for binaries and configuration files.

Refactoring suggestions and / or pull requests are welcome!

### Prerequisites

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


### Building + Installing

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


[img_autotx_logo]: https://git.scicore.unibas.ch/vamp/auto-tx/raw/master/Resources/auto-tx-logo.png

[web_imcf]: https://www.biozentrum.unibas.ch/imcf
[web_bioz]: https://www.biozentrum.unibas.ch/
[web_unibas]: https://www.unibas.ch/
[web_robosharp]: https://github.com/tjscience/RoboSharp
[web_robosharp_fork]: https://git.scicore.unibas.ch/vamp/robosharp
[web_nlog]: http://nlog-project.org/