# Mounts a Windows ISO and finds the image in it that matches this PC's edition, as a DISM /Source.
# Parameters: $Path

$image = Get-DiskImage -ImagePath $Path
$mounted = -not $image.Attached
if ($mounted) { $image = Mount-DiskImage -ImagePath $Path -StorageType ISO -Access ReadOnly -PassThru }

$letter = ($image | Get-Volume).DriveLetter
$file = @("${letter}:\sources\install.wim", "${letter}:\sources\install.esd") | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $file) {
    Emit @{ state = 'noimage'; mounted = $mounted }
    exit 0
}

$edition = (Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion').EditionID
$match = Get-WindowsImage -ImagePath $file |
    ForEach-Object { Get-WindowsImage -ImagePath $file -Index $_.ImageIndex } |
    Where-Object { $_.EditionId -eq $edition } |
    Select-Object -First 1
if (-not $match) {
    Emit @{ state = 'noedition'; edition = $edition; mounted = $mounted }
    exit 0
}

$format = if ($file.EndsWith('.esd')) { 'ESD' } else { 'WIM' }
Emit @{ state = 'ready'; source = "${format}:${file}:$($match.ImageIndex)"; mounted = $mounted }
