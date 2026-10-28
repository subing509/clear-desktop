$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'installed'
[IO.Directory]::CreateDirectory($output)|Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$automation=Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL\System.Management.Automation\v4.0_3.0.0.0__31bf3856ad364e35\System.Management.Automation.dll'
if(-not(Test-Path $compiler) -or -not(Test-Path $automation)){throw 'Windows PowerShell 5.1 and .NET Framework 4.x are required.'}
& $compiler /nologo /target:winexe "/out:$output\ClearDesktop.exe" "/reference:$automation" (Join-Path $PSScriptRoot 'native-preview\InstalledHost.cs')
if($LASTEXITCODE -ne 0){throw 'Host build failed. Close Clear Desktop before rebuilding.'}
& $compiler /nologo /target:winexe "/out:$output\WindowRecoveryGuard.exe" (Join-Path $PSScriptRoot 'native-preview\WindowRecoveryGuard.cs')
if($LASTEXITCODE -ne 0){throw 'Recovery guard build failed.'}
foreach($file in @('Start-ClearDesktop.ps1','NativeIcons.cs','NativeMotion.cs','NativeGenie.cs')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot "native-preview\$file") -Destination $output}
Write-Output "Built: $output\ClearDesktop.exe"
