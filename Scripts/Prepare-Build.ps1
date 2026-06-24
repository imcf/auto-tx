# Helper script to be called by Visual Studio as a "pre-build" command.

[CmdletBinding(DefaultParameterSetName = "build")]
Param(
    [Parameter(Mandatory = $True, ParameterSetName = "build")]
    [Parameter(Mandatory = $True, ParameterSetName = "gentemplate")]
    [string] $SolutionDir
    ,
    [Parameter(Mandatory = $True, ParameterSetName = "build")]
    [ValidateSet("Debug", "Release")]
    [string] $ConfigurationName
    ,
    [Parameter(ParameterSetName = "gentemplate")]
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
        [Parameter(Mandatory = $True)]
        [String]$Target,

        [Parameter(Mandatory = $True)]
        [Hashtable]$CommitInfo,

        [Parameter(Mandatory = $True)]
        [String]$Branch,

        [Parameter(Mandatory = $True)]
        [String]$Date
    )

    Write-Output "$($Target.Substring($Target.LastIndexOf('\')+1)) -> $($Target)"
    $Code = $CsTemplate -f `
        $CommitName, `
        $CommitInfo.GitBranch, `
        $CommitInfo.GitMajor, `
        $CommitInfo.GitMinor, `
        $CommitInfo.GitPatch, `
        $Date, `
        $CommitInfo.CommitSha
    Write-Verbose "/// generated code ///`n$($Code)`n/// generated code ///`n"
    Write-Output $($Code)
    Out-File -FilePath $Target -Encoding ASCII -InputObject $Code
}

function Parse-GitDescribe([string]$CommitName) {
    Write-Verbose "Parsing 'git describe' result [$($CommitName)]..."
    $GitDescribeParts = $CommitName.Split('-')

    # Commit name must be of format "v3.2.0-0-g526824a" (three parts)
    if ($GitDescribeParts.Length -ne 3) { throw "GitDescribeParts.Length != 3 (`$GitDescribeParts = `$CommitName.Split('-')): [$($CommitName)]" }

    $VersionParts = $GitDescribeParts[0].Split('.')

    # Version string must be of format: "v1.2.3" (three parts) or "v1.2.3.a4" (four parts)
    if ($VersionParts.Length -lt 3) { throw "VersionParts.Length < 3  (`$VersionParts=`$GitDescribeParts[0].Split('.')); GitDescribeParts[0]: $($GitDescribeParts[0])" }
    if ($VersionParts.Length -gt 4) { throw "VersionParts.Length > 4  (`$VersionParts=`$GitDescribeParts[0].Split('.')); GitDescribeParts[0]: $($GitDescribeParts[0])" }

    $CommitInfo = @{
        CommitName            = $CommitName
        GitMajor              = $VersionParts[0].TrimStart('v')
        GitMinor              = $VersionParts[1]
        GitPatch              = $VersionParts[2]
        NumberCommitsAfterTag = $GitDescribeParts[1]
        CommitSha             = $GitDescribeParts[2]
    }

    Return $CommitInfo
}


$ErrorActionPreference = "Stop"

Push-Location $SolutionDir -ErrorAction Stop

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
    $CommitName = & git describe --tags --long --match "v[0-9]*.[0-9]*.[0-9]*"
    $GitDescribeOutput = $CommitName
    if (-Not $?) { throw }
    # $GitStatus = & git status --porcelain
    # if (-Not $?) { throw }
    # $GitBranch = & git symbolic-ref --short HEAD
    # if (-Not $?) { throw }
    $GitBranch = "fakebranch"

    $CommitInfo = Parse-GitDescribe $CommitName


    # if ($GitStatus.Length -gt 0) {
    #     $StatusWarning = "  <--  WARNING, repository has uncommitted changes!"
    #     $CommitName += "-unclean"
    # }
} catch {
    Write-Error "An error occurred: $($_.Exception.Message)"
    Exit 1
}


$Date = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'

# the dotted short format can be used in the AssemblyInformationalVersion
# property, as this will magically be parsed and reported as "Version" when
# examining the executable using "Get-Command AutoTx.exe | Format-List *"
$DateShort = Get-Date -Format 'yyyy.MM.dd.HHmm'


# use "-EA Ignore" to prevent build issues when VS is building multiple projects
# at the same time that all use this script (leading to "file already in use"
# errors), where they would all produce the same output anyway:
$Date | Out-File -Force $BuildDate -ErrorAction Ignore
$CommitName | Out-File -Force $BCommit -ErrorAction Ignore
$ConfigurationName | Out-File -Force $BuildConfig -ErrorAction Ignore

Write-Output $(
    "GitDescribeOutput: [$GitDescribeOutput]"
    "build-config: [$($ConfigurationName)]"
    "build-date:   [$($Date)]"
    "git-branch:   [$($GitBranch)]"
    "git-describe: [$($CommitName)]$($StatusWarning)"
)


Write-BuildDetails $BuildDetailsCS $CommitInfo $GitBranch $DateShort

Pop-Location