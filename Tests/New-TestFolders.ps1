# Create the folders AutoTx needs for the tests.

# Local folders, same setup as on production systems:
New-Item -ItemType Directory -Force -Path "D:\ProgramData\AUTOTRANSFER\INCOMING\testuser"

# Needs a temporary folder for the process to use, mimics the temp folder on the network share:
New-Item -ItemType Directory -Force -Path "D:\AutoTxTest\DESTINATION\AUTOTRANSFER-TMP"
New-Item -ItemType Directory -Force -Path "D:\AutoTxTest\DESTINATION\testuser"

# Show the paths created:
Get-ChildItem -Recurse "D:\ProgramData\AUTOTRANSFER"
Get-ChildItem -Recurse "D:\AutoTxTest"
