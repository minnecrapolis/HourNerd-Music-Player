[CmdletBinding()]
param([string]$CompilerPath, [switch]$UseExistingDistribution)
$ErrorActionPreference = 'Stop'
$options = @{ UseExistingDistribution = $UseExistingDistribution }
if ($CompilerPath) { $options.CompilerPath = $CompilerPath }
& "$PSScriptRoot\Build-Windows-Installer.ps1" @options
& "$PSScriptRoot\Build-Windows-MSI.ps1"
Write-Host 'Both formats built. Choose one installer per computer; uninstall before switching formats.'
