# settings for the AutoTx Service Updater

$ServiceName = "AutoTx"
$InstallationPath = "C:\Tools\$($ServiceName)"
$LogPath = "$($InstallationPath)"

$UpdateSourcePath = "\\fileserver.mydomain.xy\share\_AUTOTX_"
$Pattern = 'build_[0-9]{4}-[0-9]{2}-[0-9]{2}_'

$EmailFrom = "admin@mydomain.xy"
$EmailTo   = "admin@mydomain.xy"
$EmailSMTP = "smtp.mydomain.xy"