# Installs the available Windows updates one at a time through the Windows Update Agent API. Optional updates
# (BrowseOnly) and updates that need user input are left for Settings. Creating $StopFile stops the run between updates,
# so an update is never interrupted halfway.
# Parameters: $StopFile

# The update's own error, or the operation's when the update has none.
function Get-HResult($result) {
    $code = $result.GetUpdateResult(0).HResult
    if ($code) { $code } else { $result.HResult }
}

$session = New-Object -ComObject Microsoft.Update.Session
$session.ClientApplicationID = 'Refreshify'

Emit @{ phase = 'searching' }
$search = $session.CreateUpdateSearcher().Search('IsInstalled=0 and IsHidden=0 and BrowseOnly=0')
$updates = @($search.Updates | Where-Object { -not $_.InstallationBehavior.CanRequestUserInput })
Emit @{ found = $updates.Count }

$restart = $false
$index = 0
foreach ($update in $updates) {
    if (Test-Path -LiteralPath $StopFile) {
        Emit @{ stopped = $true }
        break
    }

    $index++
    if (-not $update.EulaAccepted) { $update.AcceptEula() }
    $batch = New-Object -ComObject Microsoft.Update.UpdateColl
    [void]$batch.Add($update)

    if (-not $update.IsDownloaded) {
        Emit @{ update = $update.Title; index = $index; count = $updates.Count; phase = 'downloading' }
        $downloader = $session.CreateUpdateDownloader()
        $downloader.Updates = $batch
        $download = $downloader.Download()
        # OperationResultCode: 2 succeeded, 3 succeeded with errors.
        if ($download.ResultCode -notin 2, 3) {
            Emit @{ failed = $update.Title; hresult = (Get-HResult $download) }
            continue
        }
    }

    Emit @{ update = $update.Title; index = $index; count = $updates.Count; phase = 'installing' }
    $installer = $session.CreateUpdateInstaller()
    $installer.Updates = $batch
    $installer.ForceQuiet = $true
    $install = $installer.Install()
    if ($install.ResultCode -in 2, 3) {
        $restart = $restart -or $install.RebootRequired
        Emit @{ installed = $update.Title }
    } else {
        Emit @{ failed = $update.Title; hresult = (Get-HResult $install) }
    }
}

Emit @{ done = $true; restart = $restart }
