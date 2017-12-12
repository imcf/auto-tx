# Script to be run by the task scheduler for automatic updates of the AutoTx
# service binaries and / or configuration file.

# Testing has been done on PowerShell 5.1 only, so we set this as a requirement:
#requires -version 5.1


[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)][string] $UpdaterSettings
)

try {
    . $UpdaterSettings
}
catch {
    $ex = $_.Exception.Message
    Write-Host "Error reading settings file: '$($UpdaterSettings)' [$($ex)]"
    Exit
}

$Me = $MyInvocation.MyCommand -replace '.ps1'



###  function definitions  #####################################################

function Check-ServiceState([string]$ServiceName) {
    $Continue = $True
    try {
        $Service = Get-Service $ServiceName -ErrorAction Stop
        if ($Service.Status -ne "Running") {
            Write-Host "Service $($ServiceName) is not running."
            $Continue = $False
        }
    }
    catch {
        Write-Host $_.Exception.Message
        $Continue = $False
    }
    if ($Continue) {
        Return
    }
    Log-Error "ERROR: Service '$($ServiceName)' must be installed and running."
    Exit
}


function Exit-IfDirMissing([string]$DirName, [string]$Desc) {
    if (Test-Path -PathType Container $DirName) {
        Return
    }
    $msg = "ERROR: can't find / access $($Desc) path: $($DirName)"
    Send-MailReport -Subject "path or permission error" -Body $msg
    Exit
}


function Stop-MyService {
    try {
        Stop-Service "$($ServiceName)" -ErrorAction Stop
        Write-Verbose "Stopped service $($ServiceName)."
    }
    catch {
        $ex = $_.Exception.Message
        Send-MailReport -Subject "Shutdown of service $($ServiceName) failed!" `
            -Body "Trying to stop the service results in this error:`n$($ex)"
        Exit
    }
}


function Start-MyService {
    if ((Get-Service $ServiceName).Status -eq "Running") {
        Return
    }
    try {
        Start-Service "$($ServiceName)" -ErrorAction Stop
        Write-Verbose "Started service $($ServiceName)."
    }
    catch {
        $ex = $_.Exception.Message
        Send-MailReport -Subject "Startup of service $($ServiceName) failed!" `
            -Body "Trying to start the service results in this error:`n$($ex)"        
        Exit
    }
}


function Get-WriteTime([string]$FileName) {
    try {
        $TimeStamp = (Get-Item "$FileName").LastWriteTime
    }
    catch {
        $ex = $_.Exception.Message
        Log-Error "Error determining file age of '$($FileName)'!`n$($ex)"
        Exit
    }
    Return $TimeStamp
}


function File-IsUpToDate([string]$ExistingFile, [string]$UpdateCandidate) {
    # Compare write-timestamps, return $False if $UpdateCandidate is newer.
    Write-Verbose "Comparing $($UpdateCandidate) vs. $($ExistingFile)..."
    $CandidateTime = Get-WriteTime -FileName $UpdateCandidate
    $ExistingTime = Get-WriteTime -FileName $ExistingFile
    if ($CandidateTime -le $ExistingTime) {
        Write-Verbose "File $($ExistingFile) is up-to-date."
        Return $True
    }
    Return $False
}


function Create-Backup {
    Param (
        [Parameter(Mandatory=$True)]
        [ValidateScript({Test-Path -PathType Leaf $_})]
        [String]$FileName

    )
    $FileWithoutSuffix = [io.path]::GetFileNameWithoutExtension($FileName)
    $FileSuffix = [io.path]::GetExtension($FileName)
    $BaseDir = Split-Path -Parent $FileName
    
    # assemble a timestamp string like "2017-12-04T16.41.35"
    $BakTimeStamp = Get-Date -Format s | foreach {$_ -replace ":", "."}
    $BakName = "$($FileWithoutSuffix)_pre-$($BakTimeStamp)$($FileSuffix)"
    Log-Info "Creating backup of '$($FileName)' as '$($BaseDir)\$($BakName)'."
    try {
        Rename-Item "$FileName" "$BaseDir\$BakName" -ErrorAction Stop
    }
    catch {
        $ex = $_.Exception.Message
        Log-Error "Backing up '$($FileName)' as '$($BakName) FAILED!`n$($ex)"
        Exit
    }
}


function Update-File {
    # Check the given $SrcFile if a file with the same name is existing in
    # $DstPath. If $SrcFile is newer, stop the service, create a backup of the
    # file in $DstPath and finally copy the file from $SrcFile to $DstPath.
    #
    # Return $True if the file was updated, $False otherwise.
    #
    # WARNING: the function TERMINATES the script on any error!
    #
    Param (
        [Parameter(Mandatory=$True)]
        [ValidateScript({[IO.Path]::IsPathRooted($_)})]
        [String]$SrcFile,

        [Parameter(Mandatory=$True)]
        [ValidateScript({(Get-Item $_).PSIsContainer})]
        [String]$DstPath
    )

    $DstFile = "$($DstPath)\$(Split-Path -Leaf $SrcFile)"
    if (-Not (Test-Path "$DstFile")) {
        Log-Info "File not existing in destination, NOT UPDATING: $($DstFile)"
        Return $False
    }

    if (File-IsUpToDate -ExistingFile $DstFile -UpdateCandidate $SrcFile) {
        Return $False        
    }

    Log-Info "Found newer file at $($SrcFile), updating..."
    Stop-MyService

    try {
        Create-Backup -FileName $DstFile
    }
    catch {
        Log-Error "Backing up $($DstFile) FAILED!`n$($_.Exception.Message)"
        Exit
    }

    try {
        Copy-Item -Path $SrcFile -Destination $DstPath -ErrorAction Stop
        Log-Info "Updated config file '$($DstFile)'."
    }
    catch {
        Log-Error "Copying $($SrcFile) FAILED!`n$($_.Exception.Message)"
        Exit
    }
    Return $True
}


function Update-Configuration {
    $NewConfig = "$($UpdateConfigPath)\configuration.xml"
    if (Test-Path -PathType Leaf $NewConfig) {
        $ret = Update-File $NewConfig $InstallationPath
    } else {
        $ret = $False
        Write-Verbose "No configuration file found at '$($NewConfig)'."
    }
    Return $ret
}


function Copy-ServiceFiles {
    try {
        Copy-Item -Recurse -Force -ErrorAction Stop `
            -Path "$($UpdateBinariesPath)\$($ServiceName)\*" `
            -Destination "$InstallationPath"
    }
    catch {
        Log-Error "Updating service binaries FAILED!`n$($_.Exception.Message)"
        Exit
    }
    Log-Info "Updated service binaries!"
}


function Update-ServiceBinaries {
    $MarkerFile = "$($UpdateMarkerPath)\$($env:COMPUTERNAME)"
    if (Test-Path "$MarkerFile" -Type Leaf) {
        Write-Verbose "Found marker ($($MarkerFile)), not updating service."
        Return $False
    }
    Log-Info "Marker file ($($MarkerFile)) missing, trying to update service."
    Stop-MyService
    Copy-ServiceFiles
    try {
        New-Item -Type File "$MarkerFile" -ErrorAction Stop | Out-Null
        Write-Verbose "Created marker file: $($MarkerFile)"
    }
    catch {
        Log-Error "Creating $($MarkerFile) FAILED!`n$($_.Exception.Message)"
        Exit
    }
    Return $True
}


function Send-MailReport([string]$Subject, [string]$Body) {
    $Subject = "[$($Me)] $($env:COMPUTERNAME) - $($Subject)"
    $msg = "------------------------------`n"
    $msg += "From: $($EmailFrom)`n"
    $msg += "To: $($EmailTo)`n"
    $msg += "Subject: $($Subject)`n`n"
    $msg += "Body: $($Body)"
    $msg += "`n------------------------------`n"
    try {
        Send-MailMessage `
            -SmtpServer $EmailSMTP `
            -From $EmailFrom `
            -To $EmailTo `
            -Body $Body `
            -Subject $Subject `
            -ErrorAction Stop
        Log-Info -Message "Sent Mail Message:`n$($msg)"
    }
    catch {
        $ex = $_.Exception.Message
        $msg = "Error sending email!`n`n$($msg)"
        $msg += "Exception message: $($ex)"
        Log-Error -Message $msg
    }
}


function Log-Message([string]$Type, [string]$Message, [int]$Id){
     $msg = "[$($Me)] "
     try {
         Write-EventLog `
            -LogName Application `
            -Source "AutoTx" `
            -Category 0 `
            -EventId $Id `
            -EntryType $Type `
            -Message "[$($Me)]: $($Message)" `
            -ErrorAction Stop
        $msg += "Logged message (Id=$($Id), Type=$($Type)).`n"
        $msg += "--- Log Message ---`n$($Message)`n--- Log Message ---`n"
     }
     catch {
         $ex = $_.Exception.Message
         $msg += "Error logging message (Id=$($Id), Type=$($Type))!`n"
         $msg += "--- Log Message ---`n$($Message)`n--- Log Message ---`n"
         $msg += "--- Exception ---`n$($ex)`n--- Exception ---"
     }
     Write-Verbose $msg
}


function Log-Error([string]$Message){
    Log-Message -Type Error -Message $Message -Id 1
}


function Log-Info([string]$Message) {
    Log-Message -Type Information -Message $Message -Id 1
}

################################################################################



# first check if the service is installed and running at all
Check-ServiceState $ServiceName

$UpdateConfigPath = "$($UpdateSourcePath)\Configs\$($env:COMPUTERNAME)"
$UpdateMarkerPath = "$($UpdateSourcePath)\Service\UpdateMarkers"
$UpdateBinariesPath = "$($UpdateSourcePath)\Service\Binaries\Latest"

Exit-IfDirMissing $InstallationPath "installation"
Exit-IfDirMissing $UpdateSourcePath "update source"
Exit-IfDirMissing $UpdateConfigPath "configuration update"
Exit-IfDirMissing $UpdateMarkerPath "update marker"
Exit-IfDirMissing $UpdateBinariesPath "service binaries update"

$ConfigUpdated = Update-Configuration
$ServiceUpdated = Update-ServiceBinaries

$msg = ""
if ($ConfigUpdated) {
    $msg += "The configuration files were updated.`n"
}
if ($ServiceUpdated) {
    $msg += "The service binaries were updated.`n"
}

if ($msg -ne "") {
    Start-MyService
    Send-MailReport -Subject "Config and / or service has been updated!" `
        -Body $msg
}
