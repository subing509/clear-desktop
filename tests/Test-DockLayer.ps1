$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$root=Split-Path $PSScriptRoot -Parent
$output=Join-Path $root '.publish-check\DockLayerTests.exe'
[IO.Directory]::CreateDirectory((Split-Path $output))|Out-Null
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references=@([Windows.Window].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.DependencyObject].Assembly.Location)
$arguments=@('/nologo','/platform:x64','/reference:System.Xaml.dll',('/out:'+$output))
foreach($reference in $references){$arguments+=('/reference:'+$reference)}
$arguments+=@((Join-Path $root 'native-preview\NativeIcons.cs'),(Join-Path $PSScriptRoot 'DockLayerTests.cs'))
& $compiler @arguments
if($LASTEXITCODE -ne 0){throw 'Layer test build failed'}
& $output
if($LASTEXITCODE -ne 0){throw 'Layer regression checks failed'}
