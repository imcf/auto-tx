# Tests

Automated tests that install AutoTx on a fresh Windows machine on GitHub and transfer real files with it.
They run with the workflow `.github/workflows/test.yml`.

## When they run

- on every pull request into `devel` or `master`
- by hand: GitHub → Actions → "Tests" → "Run workflow"

The result shows up as ✅ or ❌ on the pull request.

While working on a feature, open a **draft pull request** early: every push to the branch then runs the
tests, and nobody can merge it before you mark it "Ready for review".

## What happens

The workflow has two jobs, each on its own fresh Windows machine (`windows-2022`):

1. **build**: builds AutoTx and makes the installation package (`Scripts/Make-Package.ps1`), the same
   way as `release-build.yml`. The package zip is handed to the next job.
2. **autotx-test**: unpacks the package and runs the scripts in this folder, in this order:

| script | what it does |
|---|---|
| `Show-RunnerInfo.ps1` | prints the machine name, user, admin rights, drives and tools |
| `New-TestFolders.ps1` | creates the folders AutoTx needs on `D:` |
| `Install-AutoTx.ps1` | creates a local service account and installs AutoTx with the normal `Install-Service.ps1` from the package, using `conf/test-config.xml` as config |
| `Start-AutoTx.ps1` | starts the service and prints its log, fails if AutoTx isn't running |
| `Test-CopyFolder.ps1` | puts a folder with random files into `INCOMING\testuser`, waits until it reaches `DONE` and compares the checksums with the destination |

At the end, the AutoTx log is always printed, regardless of success or failure.

## The test setup

Same local folders as on our production machines; everything on the runner's `D:` drive:

```
D:\ProgramData\AUTOTRANSFER\
├── INCOMING\testuser\          the tests drop their data here
└── PROCESSING, DONE, ERROR …   created by AutoTx itself

D:\AutoTxTest\DESTINATION\      file server mimic
├── AUTOTRANSFER-TMP\           temp folder
└── testuser\                   the user's folder on the network share
```

`conf/test-config.xml` differs from production in a few places:

- no `SmtpHost`: mails are only written to the AutoTx log
- very high CPU and disk limits, so the busy runner doesn't pause transfers
- the destination is a local folder instead of a UNC path

The warnings about these settings in the AutoTx log are expected, as well as the warning that AutoTx wasn't correctly shut down previously.

## Adding a test

1. Copy `Test-CopyFolder.ps1` and change what it does, e.g. different files or an interruption.
   Use a folder name of its own (not `copytest`), so tests don't get in each other's way.
2. End the script with `exit 1` if the test fails. That turns the step red.
3. Add a step for it in `.github/workflows/test.yml`, after "Copy a folder".

## When a run fails

1. Open the run on GitHub (Actions tab, or the ❌ on the commit) and the job "Install and test AutoTx".
2. The red step shows what failed, e.g. `MISSING` or `DIFFERENT` for a file.
3. The step "Show the AutoTx log" shows what AutoTx did: whether it found the folder, started the
   transfer and where it moved the data.
