# Resets Windows Update: its services are stopped, its cache folders are set aside as *.bak (replacing older backups)
# so Windows builds new ones, and the services are started again even if a step fails.

$services = 'wuauserv', 'bits', 'cryptsvc'
$folders = "$env:SystemRoot\SoftwareDistribution", "$env:SystemRoot\System32\catroot2"

try {
    Stop-Service -Name $services -Force
    foreach ($folder in $folders) {
        if (Test-Path -LiteralPath "$folder.bak") { Remove-Item -LiteralPath "$folder.bak" -Recurse -Force }
        if (Test-Path -LiteralPath $folder) { Rename-Item -LiteralPath $folder -NewName "$(Split-Path $folder -Leaf).bak" }
    }
} finally {
    Start-Service -Name $services -ErrorAction SilentlyContinue
}

Emit @{ state = 'reset' }
