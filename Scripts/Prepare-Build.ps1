[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)][string] $ProjectDir
)

$oldpwd = pwd
cd $ProjectDir -ErrorAction Stop

try {
    $CommitName = git describe --tags
    $GitStatus = git status --porcelain
}
catch {
    $CommitName = "commit unknown"
    $GitStatus = "status unknown"
}


if ($GitStatus.Length -gt 0) {
    Write-Output "NOTE: repository has uncommitted changes!"
    $CommitName = "$($CommitName)-unclean"
}

$Date = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'


$BCommit = "$($ProjectDir)\Resources\BuildCommit.txt"
$BuildDate = "$($ProjectDir)\Resources\BuildDate.txt"

Write-Output $CommitName > $BCommit
Write-Output $Date > $BuildDate

Write-Output $Date
Write-Output $CommitName

cd $oldpwd