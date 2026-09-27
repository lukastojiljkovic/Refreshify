# Updates Microsoft Defender Antivirus definitions or runs a quick scan. Skipped when Defender isn't the active
# antivirus: another product has put it in passive mode, or its service isn't running.
# Parameters: $Action ('update' or 'scan')

$status = try { Get-MpComputerStatus } catch { $null }
if (-not $status -or -not $status.AntivirusEnabled -or ($status.AMRunningMode -and $status.AMRunningMode -ne 'Normal')) {
    Emit @{ skipped = 'Microsoft Defender Antivirus isn''t the active antivirus on this PC.' }
    exit 0
}

if ($Action -eq 'update') {
    Update-MpSignature
    Emit @{ version = (Get-MpComputerStatus).AntivirusSignatureVersion }
    exit 0
}

$start = Get-Date
Start-MpScan -ScanType QuickScan
$threats = @(Get-MpThreatDetection | Where-Object { $_.InitialDetectionTime -ge $start })
$names = @($threats | ForEach-Object { (Get-MpThreat -ThreatID $_.ThreatID).ThreatName } | Sort-Object -Unique)
Emit @{ threats = $threats.Count; names = $names }
