# Copy test: put a folder with random files into INCOMING and check that AutoTx delivers it unchanged.

$Incoming = "D:\ProgramData\AUTOTRANSFER\INCOMING\testuser"
$DoneDir = "D:\ProgramData\AUTOTRANSFER\DONE\testuser"
$ErrorDir = "D:\ProgramData\AUTOTRANSFER\ERROR"
$Destination = "D:\AutoTxTest\DESTINATION\testuser\copytest"
$Staging = "D:\AutoTxTest\staging\copytest"

$FileNames = "01_small.dat", "02_medium.dat", "subfolder\03_large.dat"
$FileSizesMB = 1, 20, 100


# Write a file of the given size with random content (random data can't be compressed, like real images):
function New-RandomFile($Path, $SizeMB) {
    $Bytes = New-Object byte[] ($SizeMB * 1MB)
    (New-Object System.Random).NextBytes($Bytes)
    [System.IO.File]::WriteAllBytes($Path, $Bytes)
}


# 1. Create the test files outside of INCOMING, AutoTx would pick up a half-written folder:
New-Item -ItemType Directory -Force -Path "$Staging\subfolder"
for ($i = 0; $i -lt $FileNames.Count; $i++) {
    New-RandomFile "$Staging\$($FileNames[$i])" $FileSizesMB[$i]
    Write-Host "Created $($FileNames[$i]) ($($FileSizesMB[$i]) MB)"
}


# 2. Submit the folder. Moving within the same drive is instant, so AutoTx sees it complete:
Move-Item $Staging "$Incoming\copytest"
Write-Host "Submitted the folder, waiting for AutoTx..."


# 3. Wait until AutoTx has moved the source to DONE, its last step. Check every 5 seconds, 120 times (10 minutes):
$Finished = $false
foreach ($Try in 1..120) {
    if (Test-Path "$DoneDir\*\copytest") {
        $Finished = $true
        break
    }
    if (Test-Path "$ErrorDir\*") {
        Write-Host "FAIL: the transfer ended up in ERROR"
        exit 1
    }
    Start-Sleep -Seconds 5
}

if (-not $Finished) {
    Write-Host "FAIL: the transfer did not finish within 10 minutes"
    exit 1
}
Write-Host "Transfer finished"


# 4. Compare each delivered file with the source (now in DONE) using a checksum:
$Source = Resolve-Path "$DoneDir\*\copytest"
$Failures = 0

foreach ($Name in $FileNames) {
    $SourceFile = "$Source\$Name"
    $TargetFile = "$Destination\$Name"

    if (-not (Test-Path $TargetFile)) {
        Write-Host "MISSING:   $Name"
        $Failures = $Failures + 1
        continue
    }

    $SourceHash = (Get-FileHash $SourceFile).Hash
    $TargetHash = (Get-FileHash $TargetFile).Hash
    if ($SourceHash -eq $TargetHash) {
        Write-Host "OK:        $Name"
    } else {
        Write-Host "DIFFERENT: $Name"
        $Failures = $Failures + 1
    }
}

if ($Failures -gt 0) {
    Write-Host "FAIL: $Failures file(s) missing or different"
    exit 1
}
Write-Host "PASS: all files arrived unchanged"
