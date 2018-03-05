function Stop-MyService {
    Write-Host -NoNewLine "Checking status of $($ServiceName) service: "
    try {
        $Service = Get-Service $ServiceName -ErrorAction Stop
        Write-Host $Service.Status
    }
    catch {
        Write-Host "[FAILED]" -Fore Red
        Write-Host "Can't query the service state, stopping."
        Exit
    }

    if ($Service.Status -ne "Stopped") {
        Write-Host -NoNewLine "Stopping service $($ServiceName): "
        try {
            Stop-Service $ServiceName -ErrorAction Stop
            Write-Host "[OK]" -Fore Green
        }
        catch {
            Write-Host "[FAILED]" -Fore Red
        }
    }
}


function Start-MyService {
    Write-Host -NoNewLine "Starting service $($ServiceName): "
    try {
        Start-Service $ServiceName -ErrorAction Stop
        Write-Host "[OK]" -Fore Green
    }
    catch {
        $ex = $_.Exception
        Write-Host "[FAILED]" -Fore Red
        Write-Host $ex.Message
        Write-Host "Please check if your configuration is valid!"
        Write-Host "Showing last 20 lines of service log file:"
        Write-Host "=========================================="
        Get-Content "$($ServiceDir)\service.log" -Tail 20
    }
}


function Copy-FileIfNew([string]$SourceFile, [string]$Destination) {
    # SourceFile is expected to be a FILE name
    # Destination is expected to be a PATH
    if (Test-Path "$Destination\$SourceFile") {
        return
    }
    try {
        Copy-Item -Path $SourceFile -Destination $Destination -ErrorAction Stop
    }
    catch {
        $ex = $_.Exception
        Write-Host "Copying $($SourceFile) FAILED!" -Fore Red
        Write-Host $ex.Message
        Exit
    }
}


function Copy-ServiceFiles {
    Write-Host -NoNewLine "Updating / installing service files: "
    $TargetDir = New-Item -ItemType Container -Force -Path $ServiceDir
    try {
        Copy-Item -Recurse -Force -Path "$ServiceName\*" -Destination $ServiceDir -ErrorAction Stop
        Copy-FileIfNew "configuration.xml" $ServiceDir
        Copy-FileIfNew "status.xml" $ServiceDir
        Copy-FileIfNew "service.log" $ServiceDir
        Clear-Content "$($ServiceDir)\service.log"
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


$LocalConfiguration = ".\ScriptsConfig.ps1"
if (Test-Path $LocalConfiguration) {
    . $LocalConfiguration
} else {
    Write-Host "Can't find configuration '$LocalConfiguration'!" -Fore Red
    Exit
}


try {
    $Service = Get-Service $ServiceName -ErrorAction Stop
    Write-Host "Service $($ServiceName) already installed, updating."
    Stop-MyService
    Copy-ServiceFiles
}
catch {
    Copy-ServiceFiles
    Install-Service
}

Start-MyService
