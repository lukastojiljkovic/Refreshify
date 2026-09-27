# Empties the Delivery Optimization cache and reports the free space it returned to the system drive.

$drive = $env:SystemDrive.TrimEnd(':')
$before = (Get-Volume -DriveLetter $drive).SizeRemaining
Delete-DeliveryOptimizationCache -Force
$after = (Get-Volume -DriveLetter $drive).SizeRemaining
Emit @{ freed = [Math]::Max(0, $after - $before) }
