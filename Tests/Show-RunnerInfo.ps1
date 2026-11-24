# Print what the GitHub Windows runner looks like: who we are, our rights, drives and installed tools.

$User = [Security.Principal.WindowsIdentity]::GetCurrent()
$IsAdmin = (New-Object Security.Principal.WindowsPrincipal $User).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

Write-Host "Computer name:      $env:COMPUTERNAME"
Write-Host "Windows version:    $((Get-CimInstance Win32_OperatingSystem).Caption)"
Write-Host "PowerShell version: $($PSVersionTable.PSVersion)"
Write-Host "Running as user:    $($User.Name)"
Write-Host "Administrator:      $IsAdmin"
Write-Host ""

Write-Host "Drives:"
Get-CimInstance Win32_LogicalDisk | ForEach-Object {
    $FreeGB = [Math]::Round($_.FreeSpace / 1GB, 1)
    $SizeGB = [Math]::Round($_.Size / 1GB, 1)
    # DriveType 3 = local fixed drive, the only kind AutoTx accepts as SourceDrive
    Write-Host "  $($_.DeviceID)  type $($_.DriveType)  $FreeGB GB free of $SizeGB GB"
}
Write-Host ""

Write-Host "Tools AutoTx needs:"
$InstallUtil = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe"
Write-Host "  InstallUtil.exe:  $(Test-Path $InstallUtil)"
Write-Host "  robocopy.exe:     $([bool](Get-Command robocopy.exe -ErrorAction SilentlyContinue))"
