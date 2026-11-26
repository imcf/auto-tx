# Start the AutoTx service and show its log, so we can see how the startup went.

$LogFile = "C:\Tools\AutoTx\var\$env:COMPUTERNAME.AutoTx.log"

# if starting fails we still want to see the log, so don't stop here on an error:
Start-Service AutoTx -ErrorAction Continue

# give the service some time to read its config and check its folders:
Start-Sleep -Seconds 15

Get-Service AutoTx
Get-Content $LogFile

if ((Get-Service AutoTx).Status -ne "Running") {
    Write-Host "AutoTx is not running"
    exit 1
}
Write-Host "AutoTx is running"
