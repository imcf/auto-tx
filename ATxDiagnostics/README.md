# AutoTx Diagnostics

The `AutoTxDiagnostics` tool can be used to show some diagnostics information
and continously print performance monitoring messages to the console. With the
latter it allows to investigate performance-critical situations without the need
to actually run the `AutoTx` service itself.

## Basic Usage

By default, the log level for messages is set to *Debug* and ***all*** available
performance monitors will be enabled. For this, simply run the tool:

```
.\AutoTxDiagnostics.exe
```

## Advanced Usage

To run the tool with log level *Trace*, simply add `trace` as the first
parameter on the command line when starting:

```
.\AutoTxDiagnostics.exe trace
```

The second parameter can be used to explicitly request a specific combination of
performance monitors to be active. To run for example only the *Disk I/O*
monitoring, start the tool like this:

```
.\AutoTxDiagnostics.exe trace PhysicalDisk
```

Combinations of monitors have to be separated by a comma, for example:

```
.\AutoTxDiagnostics.exe trace PhysicalDisk,CPU
```

## Exit Codes

The diagnostics tool can be terminated by pressing `Ctrl+C`.