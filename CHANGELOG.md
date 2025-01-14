# Changelog

This is the changelog for the `auto-tx` project. See the [README](README.md)
file for more information.

All notable changes to this project must be documented in this file.

The format is based on [Keep a Changelog][0] and the file is parsed with the
["extract-release-notes GitHub action"][1].

The project adheres to [Semantic Versioning][3] (after version 3.1).

## [Unreleased]

### Added

- Build project build with GitHub Actions.
- Add CHANGELOG.md file to the project.

### Changed

### Removed

## [3.1] - 2018-03-14

### Added

- The CPU load monitoring is now several magnitudes faster and is running in a
  separate thread, allowing for very short reaction time.
- If the service is suspended for more than one reason, report all of them in
  notifications.
- All files that the service is writing to while operating (status, log files,
  ...) are now placed in a var/ subdirectory.
- Always include the friendly host name when sending emails.

### Fixed

- If the full name for a user can't be looked up, use the username instead of an
  empty string in notifications.
- Fix low-disk-space check not triggering correctly due to units confusion.

### Changed

### Removed

## [3.0] - 2018-03-14

- No changelog available.

## [2.1] - 2018-03-07

### Added

- Build-time version information, including git tags, commit IDs and branch name
  is now automatically included in the assembly information and available for
  notifications.
- The exact log level is now adjustable in the configuration file.
- Plenty of documentation updates regarding installation, updating,
  configuration, etc.
- The updater now registers the tray app for autostart on user login.
- Emails now showing the versions of AutoTx and Robosharp in their signature.
- Logging / debugging the RoboSharp commands can now be enabled in the
  configuration file.
- Failed transfers will be moved to an "error" location to prevent the service
  from subsequent attempts in transferring them again.
- Directories in the "incoming" location that cannot be processed by the service
  (permissions, ...) will be ignored until the next service restart.
- Empty directories (including nested) will not trigger a new transfer.

### Fixed

- RoboCopy flags were fixed to make the service also work on Windows Server 2012
  R2 / Windows 8.1.
- Fixed the waiting time between two subsequent transfers.

### Changed

### Removed

## [2.0] - 2018-02-20

- No changelog available.

## [1.4] - 2018-02-01

- No changelog available.

## [1.3] - 2018-01-17

- No changelog available.

## [1.2] - 2017-12-20

- No changelog available.

## [1.1] - 2017-11-02

- No changelog available.

## [1.0] - 2017-09-14

- No changelog available.

[unreleased]: https://github.com/imcf/auto-tx/compare/3.1...HEAD
[3.1]: https://github.com/imcf/auto-tx/compare/3.0...3.1
[3.0]: https://github.com/imcf/auto-tx/compare/2.1...3.0
[2.1]: https://github.com/imcf/auto-tx/compare/2.0...2.1
[2.0]: https://github.com/imcf/auto-tx/compare/1.4...2.0
[1.4]: https://github.com/imcf/auto-tx/compare/1.3...1.4
[1.3]: https://github.com/imcf/auto-tx/compare/1.2...1.3
[1.2]: https://github.com/imcf/auto-tx/compare/1.1...1.2
[1.1]: https://github.com/imcf/auto-tx/compare/1.0...1.1
[1.0]: https://github.com/imcf/auto-tx/tree/1.0
[0]: https://keepachangelog.com/en/1.1.0/
[1]: https://github.com/marketplace/actions/extract-release-notes
[3]: https://semver.org/spec/v2.0.0.html
