using System;
using System.IO;
using System.Management;
using ATXCommon;
using RoboSharp;

namespace AutoTx
{
    public partial class AutoTx
    {
        private RoboCommand _roboCommand;

        /// <summary>
        /// Start transferring data from a given source directory to the destination
        /// location that is stored in CurrentTargetTmp. Requires CopyState to be in
        /// status "Stopped", sets CopyState to "Active" and FilecopyFinished to
        /// false. The currently processed path is stored in the global status
        /// variable CurrentTransferSrc.
        /// </summary>
        private void StartTransfer(string sourcePath) {
            // only proceed when in a valid state:
            if (_transferState != TxState.Stopped)
                return;

            _status.CurrentTransferSrc = sourcePath;
            _status.CurrentTransferSize = FsUtils.GetDirectorySize(sourcePath);

            // the user name is expected to be the last part of the path:
            _status.CurrentTargetTmp = new DirectoryInfo(sourcePath).Name;
            CreateNewDirectory(ExpandCurrentTargetTmp(), false);

            _transferState = TxState.Active;
            _status.TransferInProgress = true;
            try {
                // events
                _roboCommand.OnCopyProgressChanged += RsProgressChanged;
                _roboCommand.OnFileProcessed += RsFileProcessed;
                _roboCommand.OnCommandCompleted += RsCommandCompleted;

                // copy options
                _roboCommand.CopyOptions.Source = sourcePath;
                _roboCommand.CopyOptions.Destination = ExpandCurrentTargetTmp();

                // limit the transfer bandwidth by waiting between packets:
                _roboCommand.CopyOptions.InterPacketGap = _config.InterPacketGap;

                // /S :: copy Subdirectories, but not empty ones
                // _roboCommand.CopyOptions.CopySubdirectories = true;

                // /E :: copy subdirectories, including Empty ones
                _roboCommand.CopyOptions.CopySubdirectoriesIncludingEmpty = true;

                // /PF :: check run hours on a Per File (not per pass) basis
                // _roboCommand.CopyOptions.CheckPerFile = true;

                // /SECFIX ::  fix file security on all files, even skipped files
                // _roboCommand.CopyOptions.FixFileSecurityOnAllFiles = true;

                // copyflags :
                //     D=Data, A=Attributes, T=Timestamps
                //     S=Security=NTFS ACLs, O=Owner info, U=aUditing info

                // /SEC :: copy files with security (equivalent to /COPY:DATS)
                // _roboCommand.CopyOptions.CopyFilesWithSecurity = true;
                // /COPYALL :: copy all file info (equivalent to /COPY:DATSOU)
                // _roboCommand.CopyOptions.CopyAll = true;
                _roboCommand.CopyOptions.CopyFlags = "DATO";

                // select options
                _roboCommand.SelectionOptions.ExcludeOlder = true;
                // retry options
                _roboCommand.RetryOptions.RetryCount = 0;
                _roboCommand.RetryOptions.RetryWaitTime = 2;
                _roboCommand.Start();
                writeLogDebug("Transfer started, total size: " + 
                    _status.CurrentTransferSize / MegaBytes + " MB");
            }
            catch (ManagementException ex) {
                writeLog("Error in StartTransfer(): " + ex.Message);
            }
        }

        /// <summary>
        /// Pause a running transfer.
        /// </summary>
        private void PauseTransfer() {
            // only proceed when in a valid state:
            if (_transferState != TxState.Active)
                return;

            writeLog("Pausing the active transfer...");
            _roboCommand.Pause();
            _transferState = TxState.Paused;
            writeLogDebug("Transfer paused");
        }

        /// <summary>
        /// Resume a previously paused transfer.
        /// </summary>
        private void ResumePausedTransfer() {
            // only proceed when in a valid state:
            if (_transferState != TxState.Paused)
                return;

            writeLog("Resuming the paused transfer...");
            _roboCommand.Resume();
            _transferState = TxState.Active;
            writeLogDebug("Transfer resumed");
        }

        #region robocommand callbacks

        /// <summary>
        /// RoboSharp OnFileProcessed callback handler.
        /// </summary>
        private void RsFileProcessed(object sender, FileProcessedEventArgs e) {
            try {
                var processed = e.ProcessedFile;
                // WARNING: RoboSharp doesn't seem to offer a culture invariant representation
                // of the FileClass, so this might fail in non-english environments:
                if (processed.FileClass.ToLower().Equals("new file")) {
                    _transferredFiles.Add(processed.Name + " (" + (processed.Size / 1048576) + " MB)");
                }
            }
            catch (Exception ex) {
                writeLog("Error in RsFileProcessed() " + ex.Message);
            }
        }

        /// <summary>
        /// RoboSharp OnCommandCompleted callback handler.
        /// </summary>
        private void RsCommandCompleted(object sender, RoboCommandCompletedEventArgs e) {
            if (_transferState == TxState.DoNothing)
                return;

            _roboCommand.Stop();
            writeLogDebug("Transfer stopped");
            _transferState = TxState.Stopped;
            _roboCommand.Dispose();
            _roboCommand = new RoboCommand();
            _status.TransferInProgress = false;
        }

        /// <summary>
        /// RoboSharp OnCopyProgressChanged callback handler.
        /// Print a log message if the progress has changed for more than 20%.
        /// </summary>
        private void RsProgressChanged(object sender, CopyProgressEventArgs e) {
            // report progress in steps of 20:
            var progress = ((int) e.CurrentFileProgress / 20) * 20;
            if (progress == _txProgress)
                return;

            _txProgress = progress;
            writeLogDebug("Transfer progress " + progress + "%");
        }

        #endregion
    }
}