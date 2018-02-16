$ResourceDir = "..\ATxService\Resources"
$RsrcDirCommon = "..\Resources"


function Highlight([string]$Message, [string]$Color = "Cyan") {
    Write-Host -NoNewline "["
    Write-Host -NoNewline -F $Color $Message
    Write-Host -NoNewline "]"

function RelToAbs([string]$RelPath) {
    Join-Path -Resolve $(Get-Location) $RelPath
}


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
$BinariesDirService = RelToAbs "..\ATxService\bin\$($BuildConfiguration)"
$BinariesDirTrayApp = RelToAbs "..\ATxTray\bin\$($BuildConfiguration)"
$BinariesDirCfgTest = RelToAbs "..\ATxConfigTest\bin\$($BuildConfiguration)"

Write-Host -NoNewline "Creating package "
Highlight $PkgDir "Red"
Write-Host " using binaries from:"
Write-Host $(Highlight $BinariesDirService "Green")
Write-Host $(Highlight $BinariesDirTrayApp "Green")
Write-Host $(Highlight $BinariesDirCfgTest "Green")
Write-Host ""

if (Test-Path $PkgDir) {
    Write-Host "Removing existing package dir [$($PkgDir)]..."
    Remove-Item -Recurse -Force $PkgDir
}

$dir = New-Item -ItemType Container -Name $PkgDir
$dir = New-Item -ItemType Container -Path $PkgDir -Name "AutoTx"
$tgt = $dir.FullName

Copy-Item -Exclude *.pdb -Recurse "$($BinariesDirService)\*" $tgt
Copy-Item -Exclude *.pdb -Recurse "$($BinariesDirTrayApp)\*" $tgt -EA Ignore
Copy-Item -Exclude *.pdb -Recurse "$($BinariesDirCfgTest)\*" $tgt -EA Ignore
# provide an up-to-date version of the example config file:
Copy-Item -Recurse "$($RsrcDirCommon)\conf" "$($tgt)\conf-example"

Copy-Item -Recurse "$($RsrcDirCommon)\conf" "$($PkgDir)"
Copy-Item "$($ResourceDir)\BuildDate.txt" "$($PkgDir)\AutoTx.log"
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

Write-Host -NoNewline "Done creating package "
Highlight $PkgDir
Write-Host -NoNewline " using config "
Highlight $BuildConfiguration
Write-Host -NoNewline " based on commit "
Highlight $BuildCommit

Write-Host -NoNewline "Location: "
Highlight "$(RelToAbs $PkgDir)"
Write-Host