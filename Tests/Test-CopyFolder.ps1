# Copy test: put a folder with random files into INCOMING and check that AutoTx delivers it unchanged.

$Incoming = "D:\ProgramData\AUTOTRANSFER\INCOMING\testuser"
$DoneDir = "D:\ProgramData\AUTOTRANSFER\DONE\testuser"
$ErrorDir = "D:\ProgramData\AUTOTRANSFER\ERROR"
$Destination = "D:\AutoTxTest\DESTINATION\testuser"
$Staging = "D:\AutoTxTest\staging\copytest"

# 1. Create the test files outside of INCOMING first, AutoTx would pick up a half-written folder.
#    Random content, so it can't be compressed (like real image data). Sizes in MB:
New-Item -ItemType Directory -Force -Path "$Staging\subfolder"
$Files = @{
    "01_small.dat" = 1
    "02_medium.dat" = 20
    "subfolder\03_large.dat" = 100
}
foreach ($Name in $Files.Keys) {
    $Bytes = New-Object byte[] ($Files[$Name] * 1MB)
    (New-Object System.Random).NextBytes($Bytes)
    [System.IO.File]::WriteAllBytes("$Staging\$Name", $Bytes)
}

# 2. Remember the checksum of every file:
$SourceHashes = @{}
foreach ($File in Get-ChildItem -Recurse -File $Staging) {
    $RelativePath = $File.FullName.Substring($Staging.Length + 1)
    $SourceHashes[$RelativePath] = (Get-FileHash $File.FullName).Hash
    Write-Host "Created $RelativePath ($($File.Length) bytes)"
}

# 3. Submit the folder. Moving within the same drive is instant, so AutoTx sees it complete:
Move-Item $Staging "$Incoming\copytest"
Write-Host "Submitted the folder, waiting for AutoTx..."

# 4. Wait until AutoTx has moved the source to DONE, which is its last step (max 10 minutes):
$Seconds = 0
while (-not (Test-Path "$DoneDir\*\copytest") -and $Seconds -lt 600) {
    if (Test-Path "$ErrorDir\*") {
        Write-Host "FAIL: the transfer ended up in ERROR"
        exit 1
    }
    Start-Sleep -Seconds 5
    $Seconds += 5
}
if (-not (Test-Path "$DoneDir\*\copytest")) {
    Write-Host "FAIL: the transfer did not finish within 10 minutes"
    exit 1
}
Write-Host "Transfer finished after about $Seconds seconds"

# 5. Compare the checksums of the delivered files with the ones from before:
$Failures = 0
foreach ($RelativePath in $SourceHashes.Keys) {
    $Target = "$Destination\copytest\$RelativePath"
    if (-not (Test-Path $Target)) {
        Write-Host "MISSING:   $RelativePath"
        $Failures++
    }
    elseif ((Get-FileHash $Target).Hash -ne $SourceHashes[$RelativePath]) {
        Write-Host "DIFFERENT: $RelativePath"
        $Failures++
    }
    else {
        Write-Host "OK:        $RelativePath"
    }
}

if ($Failures -gt 0) {
    Write-Host "FAIL: $Failures file(s) missing or different"
    exit 1
}
Write-Host "PASS: all files arrived unchanged"
