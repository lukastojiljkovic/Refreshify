# Creates a System Restore point, unless one exists from the last 24 hours: Windows itself refuses to create another
# within that window, and silently reports success when asked to.
# Parameters: $Description

$latest = try { Get-ComputerRestorePoint | Sort-Object SequenceNumber | Select-Object -Last 1 } catch { $null }
if ($latest) {
    $created = [Management.ManagementDateTimeConverter]::ToDateTime($latest.CreationTime)
    if ($created -gt (Get-Date).AddHours(-24)) {
        Emit @{ state = 'recent'; created = $created.ToString('o') }
        exit 0
    }
}

Checkpoint-Computer -Description $Description -RestorePointType MODIFY_SETTINGS

$new = try { Get-ComputerRestorePoint | Sort-Object SequenceNumber | Select-Object -Last 1 } catch { $null }
if ($new -and (-not $latest -or $new.SequenceNumber -gt $latest.SequenceNumber)) {
    Emit @{ state = 'created' }
} else {
    Emit @{ state = 'notcreated' }
}
