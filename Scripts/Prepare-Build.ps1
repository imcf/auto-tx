[CmdletBinding(DefaultParameterSetName="build")]
Param(
    [Parameter(Mandatory=$True, ParameterSetName="build")]
    [Parameter(Mandatory=$True, ParameterSetName="gentemplate")]
    [string] $SolutionDir
    ,
    [Parameter(Mandatory=$True, ParameterSetName="build")]
    [ValidateSet("Debug", "Release")]
    [string] $ConfigurationName
    ,
    [Parameter(ParameterSetName="gentemplate")]
    [switch] $GenericTemplate
)

$CsTemplate = @"
namespace ATxCommon
{{
    public static class BuildDetails
    {{
        public const string GitCommitName = "{0}";
        public const string GitBranch = "{1}";
        public const string GitMajor = "{2}";
        public const string GitMinor = "{3}";
        public const string GitPatch = "{4}";
        public const string BuildDate = "{5}";
        public const string GitCommit = "{6}";
    }}
}}
"@

function Write-BuildDetails {
    Param (
        [Parameter(Mandatory=$True)]
        [String]$Target,

        [Parameter(Mandatory=$True)]
        [Array]$Desc,

        [Parameter(Mandatory=$True)]
        [String]$Branch,

        [Parameter(Mandatory=$True)]
        [String]$Date
    )

    if ($Desc[3].Equals("nogit")) {
        $CommitName = "?commit?"
        $Commit = "?sha1"
    } else {
        $CommitName = "$($Desc[0]).$($Desc[1])-$($Desc[2])-$($Desc[3])"
        $Commit = $Desc[3].Substring(1)
    }
    Write-Output "$($Target.Substring($Target.LastIndexOf('\')+1)) -> $($Target)"
    $Code = $CsTemplate -f `
        $CommitName, `
        $Branch, `
        $Desc[0], `
        $Desc[1], `
        $Desc[2], `
        $Date, `
        $Commit
    Write-Verbose "/// generated code ///`n$($Code)`n/// generated code ///`n"
    Out-File -FilePath $Target -Encoding ASCII -InputObject $Code
}

function Parse-GitDescribe([string]$CommitName) {
    Write-Verbose "Parsing 'git describe' result [$($CommitName)]..."
    try {
        $Items = $CommitName.Split('-').Split('.')
        if ($Items.Length -ne 4) { throw }
    }
    catch {
        throw "Can't parse commit name [$($CommitName)]!"
    }
    Write-Verbose "Just some $($Items[2]) commits after the last tag."
    Return $Items
}


$ErrorActionPreference = "Stop"

$OldLocation = Get-Location
Set-Location $SolutionDir -ErrorAction Stop

$BCommit = "$($SolutionDir)\Resources\BuildCommit.txt"
$BuildDate = "$($SolutionDir)\Resources\BuildDate.txt"
$BuildConfig = "$($SolutionDir)\Resources\BuildConfiguration.txt"
$BuildDetailsCS = "$($SolutionDir)\ATxCommon\BuildDetails.cs"

if ($GenericTemplate) {
    $VerbosePreference = "Continue"
    $DescItems = "1", "0", "0", "nogit"
    Write-BuildDetails $BuildDetailsCS $DescItems "?branch?" "?build time?"
    Set-Location $OldLocation
    Exit
}

try {
    $CommitName = & git describe --tags --long --match "[0-9]*.[0-9]*"
    if (-Not $?) { throw }
    $GitStatus = & git status --porcelain
    if (-Not $?) { throw }
    $GitBranch = & git symbolic-ref --short HEAD
    if (-Not $?) { throw }

    $DescItems = Parse-GitDescribe $CommitName

    if ($GitStatus.Length -gt 0) {
        $StatusWarning = "  <--  WARNING, repository has uncommitted changes!"
        $CommitName += "-unclean"
    }
}
catch {
    $CommitName = "commit unknown"
    $GitBranch = "branch unknown"
    Write-Output "$(">"*8) Running git failed, using default values! $("<"*8)"
}


$Date = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'

# the dotted short format can be used in the AssemblyInformationalVersion
# property, as this will magically be parsed and reported as "Version" when
# examining the executable using "Get-Command AutoTx.exe | Format-List *"
$DateShort = Get-Date -Format 'yyyy.MM.dd.HHmm'


# use "-EA Ignore" to prevent build issues when VS is building multiple projects
# at the same time that all use this script (leading to "file already in use"
# errors), where they would all produce the same output anyway:
$Date | Out-File $BuildDate -ErrorAction Ignore
$CommitName | Out-File $BCommit -ErrorAction Ignore
$ConfigurationName | Out-File $BuildConfig -ErrorAction Ignore

Write-Output $(
    "build-config: [$($ConfigurationName)]"
    "build-date:   [$($Date)]"
    "git-branch:   [$($GitBranch)]"
    "git-describe: [$($CommitName)]$($StatusWarning)"
)

Write-BuildDetails $BuildDetailsCS $DescItems $GitBranch $DateShort

Set-Location $OldLocation