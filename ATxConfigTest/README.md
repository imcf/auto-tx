# AutoTx Configuration Validator

The `AutoTxConfigTest` tool can be used to validate a set of configuration files
and print a summary to the console.

## Basic Usage

By default, configuration files are expected to be in the same directory as the
`AutoTxConfigTest` executable. In that case simply run the tool like this:

```
.\AutoTxConfigTest.exe
```

## Advanced Usage

Instead of checking the config files in the directory of the executable, any
other path can be specified as the first parameter when running the
configuration validator, e.g.

```
.\AutoTxConfigTest.exe C:\Temp\new-config\
```

This also works with UNC paths, like

```
.\AutoTxConfigTest.exe \\some.file.server\share\new-config\
```

To increase the log level of the parser, a second parameter can be specified as
either `debug` or `trace`:

```
.\AutoTxConfigTest.exe  C:\Temp\new-config\  trace
.\AutoTxConfigTest.exe  .  debug
```

## Exit Codes

A valid configuration will result in exit code `0`, an invalid in `-1`.