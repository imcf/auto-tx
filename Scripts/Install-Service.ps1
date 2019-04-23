# Helper script to install the AutoTx service on a computer. It is intended to
# be run from *within* an AutoTx installation package created with the
# `Make-Package.ps1` script.
#
# NOTE: the script will NOT update an existing installation of AutoTx!

# set our requirements:
#Requires -version 5.1
#Requires -RunAsAdministrator

[CmdletBinding()]
Param(
    [Parameter(Mandatory=$False)][switch] $StartService
)

function Start-MyService {
    Write-Host -NoNewLine "Starting service $($ServiceName): "
    try {
        Start-Service $ServiceName
        Write-Host "[OK]" -Fore Green
    }
    catch {
        $ex = $_.Exception
        Write-Host "[FAILED]" -Fore Red
        Write-Host $ex.Message
        Write-Host "Please check if your configuration is valid!"
        Write-Host "Showing last 20 lines of service log file:"
        Write-Host "=========================================="
        Get-Content $ServiceLog -Tail 20
    }
}


function Copy-ServiceFiles {
    if (Test-Path -Type Container $ServiceDir) {
        Write-Host "Target directory [$($ServiceDir)] exists, stopping!"
        Exit 1
    }

    Write-Host -NoNewLine "Updating / installing service files: "
    $TargetDir = New-Item -ItemType Container -Force -Path $ServiceDir
    try {
        Copy-Item -Recurse -Force -Path "$ServiceName\*" -Destination $ServiceDir
        Copy-Item -Recurse -Force -Path "conf-example" -Destination $ServiceDir
        # create a dummy log file, so admins can already start watching it:
        Out-File -FilePath $ServiceLog -InputObject "$($ServiceName) installed"
        Write-Host "[OK]" -Fore Green
    }
    catch {
        $ex = $_.Exception
        Write-Host "[FAILED]" -Fore Red
        Write-Host $ex.Message
        Exit
    }
}


function Install-Service {
    Write-Host "Installing service $($ServiceName)... "
    Write-Host "========================================================================"
    $InstallUtil = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe"
    $ServiceExe = $ServiceDir + "\" + $ServiceName + ".exe"
    $ArgList = ("/username=$ServiceUser", "/password=$ServicePasswd", "/unattended", "$ServiceExe")
    $InstallProcess = Start-Process -FilePath "$InstallUtil" -ArgumentList $ArgList -Wait -NoNewWindow -PassThru
    Write-Host "========================================================================"
    Write-Host "InstallUtil exit code: $($InstallProcess.ExitCode)"
    Write-Host "========================================================================"
}


function Add-PerfMonGroupMember {
    $GroupName = "Performance Monitor Users"
    try {
        Add-LocalGroupMember -Group $GroupName -Member $ServiceUser
        Write-Host $("Successfully added user [$($ServiceUser)] to the local"
                     "group [$($GroupName)].")
    }
    catch [Microsoft.PowerShell.Commands.MemberExistsException] {
        Write-Host $("User [$($ServiceUser)] is already a member of the local"
                     "group [$($GroupName)], no action required.")
    }
    catch {
        Write-Host $("Adding user [$($ServiceUser)] to the local group"
                     "[$($GroupName)] failed: $($_.Exception.Message)")
    }
}


$ErrorActionPreference = "Stop"


$LocalConfiguration = ".\ScriptsConfig.ps1"
if (Test-Path $LocalConfiguration) {
    . $LocalConfiguration
} else {
    Write-Host "Can't find configuration '$LocalConfiguration'!" -Fore Red
    Exit
}

$ServiceLog = "$($ServiceDir)\var\$($env:COMPUTERNAME).$($ServiceName).log"


$Service = Get-Service $ServiceName -ErrorAction SilentlyContinue
if ($Service) {
    Write-Host "Service $($ServiceName) already installed! Please use the" `
        "Updater to do service updates. Stopping."
    Exit 1
}

Copy-ServiceFiles
Install-Service
Add-PerfMonGroupMember

Write-Host "`nWatching the service log file can be done like this:`n" `
    "`n> Get-Content -Wait -Tail 50 $($ServiceLog)`n"

if ($StartService) {
    Start-MyService    
} else {
    Write-Host "Service installation has completed.`n" `
        "`nNOTE: the service has not been started. Create and/or check`n" `
        "the configuration files and start the service manually using:`n" `
        "`n> Start-Service $($ServiceName)`n"
}