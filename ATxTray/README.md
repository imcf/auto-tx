# AutoTx Tray Monitor

This project is a complement to the AutoTx *system service* that is running in
the context of a logged-in user, delivering information and providing ways of
interacting with the service from a GUI session.

## Tray Icon Colors

The tray icon changes color depending on the state the service is currently in:

- red background: service stopped
- orange background: service running
- black arrow: service idle
- red arrow: service suspended due to system parameters
- green arrow: transfer in progress

## Tray Icon Context Menu

More details on the service status is presented in the context menu of the tray
icon (available on a right-click), including real-time information about the
progress of a running transfer.

In addition, the context menu offers a possibility for starting a new transfer
by presenting the user with a folder selection dialog. This option is also
available via a double-click on the tray icon directly.

## Balloon Tooltips

The tray app is designed to consume as little attention by the user as possible,
the only situations where it actively signifies something are when a transfer is
starting or has finished. A balloon tooltip will be shown for a short period,
informing the user about the transfer.
