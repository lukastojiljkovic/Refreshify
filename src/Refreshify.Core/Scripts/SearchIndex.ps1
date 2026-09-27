# Makes Windows Search rebuild its index from scratch the next time the service starts.

$service = Get-Service -Name WSearch -ErrorAction SilentlyContinue
if (-not $service -or $service.StartType -eq 'Disabled') {
    Emit @{ skipped = 'Windows Search is turned off on this PC.' }
    exit 0
}

Stop-Service -Name WSearch -Force
Set-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows Search' -Name SetupCompletedSuccessfully -Value 0 -Type DWord
Start-Service -Name WSearch
Emit @{ state = 'rebuilding' }
