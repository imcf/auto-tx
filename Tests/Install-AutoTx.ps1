# Install AutoTx from the downloaded package, the same way as on our production machines.

$PackageDir = "$PSScriptRoot\..\package"
$ServiceDir = "C:\Tools\AutoTx"

# AutoTx runs as a normal user account. The runner is thrown away after the run,
# so a random password is fine:
$AccountName = "autotx-svc"
$Account = "$env:COMPUTERNAME\$AccountName"
$Password = "Atx-" + [guid]::NewGuid().ToString()
$SecurePassword = ConvertTo-SecureString $Password -AsPlainText -Force
New-LocalUser -Name $AccountName -Password $SecurePassword -PasswordNeverExpires

# Install-Service.ps1 reads its settings from ScriptsConfig.ps1 in the package folder:
$Settings = @"
`$ServiceDir = "$ServiceDir"
`$ServiceName = "AutoTx"
`$ServiceUser = "$Account"
`$ServicePasswd = "$Password"
"@
Set-Content -Path "$PackageDir\ScriptsConfig.ps1" -Value $Settings

# Install-Service.ps1 uses paths relative to the package folder, so run it from there:
Set-Location $PackageDir
powershell.exe -ExecutionPolicy Bypass -File .\Install-Service.ps1

# The config file has to be named after this computer:
Copy-Item "$PSScriptRoot\conf\test-config.xml" "$ServiceDir\conf\$env:COMPUTERNAME.xml"

# The service account needs to write its log and status, and to move the test data:
icacls $ServiceDir /grant "$($Account):(OI)(CI)M" /T /Q
icacls "D:\ProgramData\AUTOTRANSFER" /grant "$($Account):(OI)(CI)M" /T /Q
icacls "D:\AutoTxTest" /grant "$($Account):(OI)(CI)M" /T /Q

# Show the result:
Get-Service AutoTx
Get-ChildItem -Recurse $ServiceDir
