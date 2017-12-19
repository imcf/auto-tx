$ResourceDir = "..\AutoTx\Resources"
$TemplateDir = "$($ResourceDir)\Mail-Templates"

try {
    $BuildDate = Get-Content "$($ResourceDir)\BuildDate.txt" -EA Stop
}
catch {
    Write-Host "Error reading build-date, stopping."
    Exit
}
try {
    $BuildConfiguration = Get-Content "$($ResourceDir)\BuildConfiguration.txt" -EA Stop
}
catch {
    Write-Host "Error reading build configuration, stopping."
    Exit
}


$PkgDir = $BuildDate -replace ':','-' -replace ' ','_'
$PkgDir = "build_" + $PkgDir
$BinariesDir = "..\AutoTx\bin\$($BuildConfiguration)"

Write-Host "Creating package [$($PkgDir)] using binaries from [$($BinariesDir)]"

if (Test-Path $PkgDir) {
    Write-Host "Removing existing package dir [$($PkgDir)]..."
    Remove-Item -Recurse -Force $PkgDir
}

$dir = New-Item -ItemType Container -Name $PkgDir
$dir = New-Item -ItemType Container -Path $PkgDir -Name "AutoTx"
$tgt = $dir.FullName

Copy-Item -Recurse "$TemplateDir" $tgt
Copy-Item -Recurse "$($BinariesDir)\*" $tgt
# provide an up-to-date version of the example config file:
Copy-Item "$($ResourceDir)\configuration-example.xml" $tgt

Copy-Item "$($ResourceDir)\configuration-example.xml" "$($PkgDir)\configuration.xml"
Copy-Item "$($ResourceDir)\status-example.xml" "$($PkgDir)\status.xml"
Copy-Item "$($ResourceDir)\BuildDate.txt" "$($PkgDir)\service.log"
Copy-Item "$($ResourceDir)\BuildConfiguration.txt" $($PkgDir)
try {
    $CommitRefFile = "$($PkgDir)\BuildCommitRef.txt"
    git describe > $CommitRefFile
    $BuildCommit = Get-Content $CommitRefFile
}
catch {
    Write-Host "Error getting commit reference from git!"
    $BuildCommit = "<UNKNOWN>"
}


Copy-Item "ScriptsConfig.ps1" $PkgDir
Copy-Item "Install-Service.ps1" $PkgDir

function Highlight([string]$Message) {
    Write-Host -NoNewline "["
    Write-Host -NoNewline -F Cyan $Message
    Write-Host -NoNewline "]"
}

Write-Host -NoNewline "Done creating package "
Highlight $PkgDir
Write-Host -NoNewline " using config "
Highlight $BuildConfiguration
Write-Host -NoNewline " based on commit "
Highlight $BuildCommit
Write-Host