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

[web_robosharp]: https://github.com/tjscience/RoboSharp
[web_robosharp_fork]: https://git.scicore.unibas.ch/vamp/robosharp