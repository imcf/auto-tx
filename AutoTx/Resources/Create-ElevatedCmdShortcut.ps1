$lnkfile = ".\elevated-cmd.lnk"

$WshShell = New-Object -comObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($lnkfile)
$Shortcut.TargetPath = "%windir%\system32\cmd.exe"
$Shortcut.Save()

$bytes = [System.IO.File]::ReadAllBytes($lnkfile)
$bytes[0x15] = $bytes[0x15] -bor 0x20     #set byte 21 (0x15) bit 6 (0x20) ON
[System.IO.File]::WriteAllBytes($lnkfile, $bytes) 