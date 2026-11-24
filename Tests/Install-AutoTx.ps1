# Install AutoTx from the downloaded package, the same way as on our production machines.

# the downloaded package contains one folder, named after the build (e.g. AutoTx-v4.0.0.a9-12-g3ab8f7a_x64):
$PackageDir = (Get-ChildItem "$PSScriptRoot\..\package" -Directory)[0].FullName
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

# The empty "conf" and "var" folders of the package get lost in the GitHub upload, but
# Install-Service.ps1 needs them (it writes the first log line into "var"):
New-Item -ItemType Directory -Force -Path "$PackageDir\AutoTx\conf"
New-Item -ItemType Directory -Force -Path "$PackageDir\AutoTx\var"

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
