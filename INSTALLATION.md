# Installation Instructions

**PLEASE NOTE:** these instructions are mostly for documentation purposes, by
default it is **strongly recommended** to use the installation script described
in [README](README.md).

## Setup



## Service Startup

Open a *PowerShell* console. To start the service and monitor its logfile, use
the following commands:
```
Start-Service AutoTx
Get-Content -Wait C:\Tools\AutoTx\service.log
```