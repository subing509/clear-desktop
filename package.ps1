param([string]$Version='0.1.1-preview')
$ErrorActionPreference='Stop'
if($Version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[a-zA-Z0-9.]+)?$'){throw 'Invalid version'}
$release=Join-Path $PSScriptRoot ('dist\'+$Version)
$stage=Join-Path $release 'payload'
[IO.Directory]::CreateDirectory($stage)|Out-Null
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory (Join-Path $stage 'installed')
foreach($file in @('README.md','LICENSE')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $stage}
[IO.File]::WriteAllText((Join-Path $stage 'VERSION.txt'),$Version,[Text.UTF8Encoding]::new($false))
$files=@('installed/ClearDesktop.exe','installed/WindowRecoveryGuard.exe','installed/Start-ClearDesktop.ps1','installed/NativeIcons.cs','installed/NativeMotion.cs','installed/NativeGenie.cs','README.md','LICENSE','VERSION.txt')
$zipPath=Join-Path $release 'ClearDesktop-Portable.zip'
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
if(Test-Path -LiteralPath $zipPath){Remove-Item -LiteralPath $zipPath}
$zip=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($file in $files){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $stage $file),$file,[IO.Compression.CompressionLevel]::Optimal)}}finally{$zip.Dispose()}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$setup=Join-Path $release 'ClearDesktop-Setup.exe'
& $compiler /nologo /target:winexe /platform:x64 /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32manifest:$PSScriptRoot\installer\app.manifest" "/resource:$zipPath,ClearDesktop.Payload" "/out:$setup" (Join-Path $PSScriptRoot 'installer\Setup.cs')
if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
$check=Start-Process -FilePath $setup -ArgumentList '--verify' -WindowStyle Hidden -Wait -PassThru
if($check.ExitCode -ne 0){throw 'Embedded installer payload verification failed'}
$checksums=@(foreach($file in @($setup,$zipPath)){$hash=Get-FileHash -LiteralPath $file -Algorithm SHA256;$hash.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($file)})
[IO.File]::WriteAllLines((Join-Path $release 'SHA256SUMS.txt'),$checksums,[Text.UTF8Encoding]::new($false))
Write-Output "Release verified: $release"
