Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Drawing,System.Windows.Forms
$ErrorActionPreference = 'Stop'
function Trace-Stage($text) { Add-Content -LiteralPath (Join-Path $PSScriptRoot 'stage.log') -Value $text }
Trace-Stage 'Assemblies loaded'
$global:cd_root = $PSScriptRoot
$global:cd_log = Join-Path $PSScriptRoot 'preview-error.log'
$global:cd_apps = [System.Collections.Generic.List[object]]::new()
# Refresh the packaged desktop entry after application updates.

$package=[pscustomobject]@{InstallLocation='';PackageFamilyName='OpenAI.Codex_2p2nqsd0c76g0'}
$active=Get-Process ChatGPT -ErrorAction SilentlyContinue | Where-Object Path | Select-Object -First 1
if($active){$package.InstallLocation=Split-Path (Split-Path $active.Path -Parent) -Parent}
if($package){
 $shell=New-Object -ComObject WScript.Shell
 try{$link=$shell.CreateShortcut((Join-Path $global:cd_root 'Codex.lnk'));$link.TargetPath=Join-Path $env:WINDIR 'explorer.exe';$link.Arguments='shell:AppsFolder\'+$package.PackageFamilyName+'!App';if($package.InstallLocation){$link.IconLocation=(Join-Path $package.InstallLocation 'app\ChatGPT.exe')+',0'};$link.Save()}finally{[void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell)}
}
$global:cd_category = '全部'
$global:cd_page = 0
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $global:cd_root 'NativeIcons.cs'))) -ReferencedAssemblies @([Windows.Window].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.DependencyObject].Assembly.Location,'System.dll','System.Core.dll')
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $global:cd_root 'NativeMotion.cs'))) -ReferencedAssemblies @([Windows.Window].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.DependencyObject].Assembly.Location,'System.dll','System.Core.dll','System.Xaml.dll')
Add-Type -TypeDefinition ([IO.File]::ReadAllText((Join-Path $global:cd_root 'NativeGenie.cs'))) -ReferencedAssemblies @([Windows.Window].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.DependencyObject].Assembly.Location,'System.dll','System.Core.dll','System.Xaml.dll','System.Drawing.dll')
$global:cd_categories = [System.Collections.Generic.List[string]]::new()
@('全部','文件','社交与娱乐','工作','其他') | ForEach-Object { $global:cd_categories.Add($_) }
function Get-Category([string]$name) {
 if ($name -match '微信|WeChat|QQ|钉钉|飞书|Teams|Discord|会议|腾讯会议') { return '社交与娱乐' }
 if ($name -match 'Steam|游戏|音乐|Music|视频|影音|抖音|剪映|播放器|Xbox') { return '社交与娱乐' }
 if ($name -match '文件|资源管理器|云盘|网盘|压缩|WinRAR|7-Zip') { return '文件' }
 if ($name -match 'Word|Excel|PowerPoint|Office|WPS|Code|Studio|记事本|终端|Notepad|画图|Adobe|Python|Codex') { return '工作' }
 return '其他'
}
$global:cd_apps.Add([pscustomobject]@{Name='资源管理器';Path=(Join-Path $env:SystemRoot 'explorer.exe');Category='文件';Icon=$null})
$paths = @([Environment]::GetFolderPath('StartMenu'),[Environment]::GetFolderPath('CommonStartMenu'),[Environment]::GetFolderPath('DesktopDirectory'))
$seen = @{}
foreach ($folder in $paths) {
 Get-ChildItem -LiteralPath $folder -Filter '*.lnk' -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
  if (-not $seen.ContainsKey($_.BaseName) -and $_.BaseName -notmatch '卸载|Uninstall|帮助|Help') {
   $seen[$_.BaseName]=$true
   $global:cd_apps.Add([pscustomobject]@{Name=$_.BaseName;Path=$_.FullName;Category=(Get-Category $_.BaseName);Icon=$null})
  }
 }
}
$global:cd_configPath=Join-Path (Split-Path $global:cd_root -Parent) 'data\settings.json'
$global:cd_saved=$null
foreach($candidate in @($global:cd_configPath,($global:cd_configPath+'.bak'))){
 if(Test-Path -LiteralPath $candidate){try{$loaded=[IO.File]::ReadAllText($candidate) | ConvertFrom-Json;if($null -eq $loaded.Apps -or $null -eq $loaded.Categories){throw 'Invalid settings'};$global:cd_saved=$loaded;break}catch{Trace-Stage ('Settings load failed: '+$candidate)}}
}
function Save-Settings {
 try{
  $folder=Split-Path $global:cd_configPath;[IO.Directory]::CreateDirectory($folder)|Out-Null
  $data=[ordered]@{Glass=(Find $global:cd_settings 'Glass').Value;Size=(Find $global:cd_settings 'Size').Value;Categories=@($global:cd_categories);PinnedPaths=@($global:cd_pinned | ForEach-Object Path);Apps=@($global:cd_apps | Select-Object Name,Path,Category)}
  $temp=$global:cd_configPath+'.tmp';$data|ConvertTo-Json -Depth 5|Set-Content -LiteralPath $temp -Encoding UTF8
  if(Test-Path -LiteralPath $global:cd_configPath){[IO.File]::Replace($temp,$global:cd_configPath,$global:cd_configPath+'.bak')}else{[IO.File]::Move($temp,$global:cd_configPath)}
 }catch{Add-Content -LiteralPath $global:cd_log -Value $_}
}
if($global:cd_saved){
 foreach($category in $global:cd_saved.Categories){if($category -and -not $global:cd_categories.Contains([string]$category)){$global:cd_categories.Add([string]$category)}}
 foreach($savedApp in $global:cd_saved.Apps){
  if(-not $savedApp.Path){continue}
  $existing=$global:cd_apps | Where-Object Path -eq $savedApp.Path | Select-Object -First 1
  if($existing){if($global:cd_categories.Contains([string]$savedApp.Category)){$existing.Category=[string]$savedApp.Category}}
  else{$global:cd_apps.Add([pscustomobject]@{Name=[string]$savedApp.Name;Path=[string]$savedApp.Path;Category=[string]$savedApp.Category;Icon=$null})}
 }
}
# Keep saved category order, including placing Other last.
if($global:cd_saved -and $global:cd_saved.Categories){
 $ordered=[Collections.Generic.List[string]]::new();$ordered.Add('全部')
 foreach($cat in @($global:cd_saved.Categories)+@($global:cd_categories)){if($cat -ne '其他' -and -not $ordered.Contains([string]$cat)){$ordered.Add([string]$cat)}}
 $ordered.Add('其他');$global:cd_categories=$ordered
}
Trace-Stage ('Apps discovered: '+$global:cd_apps.Count)
function Read-Xaml([string]$text) { [Windows.Markup.XamlReader]::Parse($text) }
$global:cd_dock = Read-Xaml @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Title="澄桌" WindowStyle="None" AllowsTransparency="True" Background="Transparent" ResizeMode="NoResize" SizeToContent="WidthAndHeight" ShowInTaskbar="False" Topmost="False" ShowActivated="False">
 <Border CornerRadius="21" Background="#A5323439" BorderBrush="#48FFFFFF" BorderThickness="1" Padding="10,14,10,10"><StackPanel Name="DockItems" Orientation="Horizontal"/></Border>
</Window>
'@
$global:cd_launcher = Read-Xaml @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Title="澄桌 · 应用启动台" Width="880" Height="570" WindowStyle="None" AllowsTransparency="True" Background="Transparent" ResizeMode="NoResize" ShowInTaskbar="False" WindowStartupLocation="Manual">
 <Border CornerRadius="28" Background="#C028292E" BorderBrush="#35FFFFFF" BorderThickness="1" Padding="32,28,32,22">
 <Grid><Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="Auto"/><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
  <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,14"><TextBox Name="Search" Width="330" Height="35" FontSize="13" Padding="12,7" Background="#18FFFFFF" Foreground="White" BorderBrush="#24FFFFFF" ToolTip="搜索应用名称"/><Button Name="SearchButton" Content="搜索" Padding="16,6" Margin="8,0,0,0" Background="#18FFFFFF" Foreground="White" BorderBrush="#24FFFFFF"/></StackPanel>
  <WrapPanel Name="Categories" Grid.Row="1" HorizontalAlignment="Center" Margin="0,0,0,12"/>
  <TextBlock Name="Results" Grid.Row="2" Foreground="#DDDDE0" HorizontalAlignment="Center" Margin="0,0,0,12"/>
  <UniformGrid Name="Apps" Grid.Row="3" Rows="3" Columns="7"/>
  <StackPanel Grid.Row="4" Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,16,0,0"><Button Name="PrevPage" Content="‹" Width="30" Height="28" Background="Transparent" Foreground="White" BorderThickness="0"/><TextBlock Name="PageNumber" Foreground="#AEB1B9" FontSize="12" VerticalAlignment="Center" Margin="18,0"/><Button Name="NextPage" Content="›" Width="30" Height="28" Background="Transparent" Foreground="White" BorderThickness="0"/></StackPanel>
 </Grid></Border>
</Window>
'@
$global:cd_settings = Read-Xaml @'
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Title="澄桌设置" Width="900" Height="680" MinWidth="780" MinHeight="580" Background="#FF25262B" Foreground="#F4F4F7" FontFamily="Segoe UI, Microsoft YaHei UI" FontSize="13" WindowStartupLocation="CenterScreen" ResizeMode="CanResize">
 <Window.Resources>
  <Style TargetType="Button"><Setter Property="Padding" Value="16,9"/><Setter Property="Margin" Value="0,4,0,4"/><Setter Property="Foreground" Value="#F2F3F8"/><Setter Property="Background" Value="#20FFFFFF"/><Setter Property="BorderBrush" Value="#25FFFFFF"/><Setter Property="BorderThickness" Value="1"/></Style>
  <Style TargetType="ComboBox"><Setter Property="Padding" Value="10,7"/><Setter Property="Foreground" Value="#25262C"/><Setter Property="Margin" Value="0,8,0,8"/></Style>
  <Style TargetType="TextBox"><Setter Property="Padding" Value="10,8"/><Setter Property="Background" Value="#12FFFFFF"/><Setter Property="Foreground" Value="White"/><Setter Property="BorderBrush" Value="#35FFFFFF"/><Setter Property="CaretBrush" Value="White"/></Style>
  <Style TargetType="Slider"><Setter Property="Margin" Value="0,15,0,15"/></Style>
  <Style x:Key="Hint" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="TextBlock"><Setter Property="Foreground" Value="#AAAFBB"/><Setter Property="TextWrapping" Value="Wrap"/><Setter Property="LineHeight" Value="22"/></Style>
  <Style x:Key="Card" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Border"><Setter Property="Background" Value="#0DFFFFFF"/><Setter Property="BorderBrush" Value="#12FFFFFF"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="14"/><Setter Property="Padding" Value="22"/><Setter Property="Margin" Value="0,18,0,0"/></Style>
 </Window.Resources>
 <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="190"/><ColumnDefinition Width="*"/></Grid.ColumnDefinitions>
  <Border Background="#FF202126" BorderBrush="#16FFFFFF" BorderThickness="0,0,1,0"><DockPanel Margin="16,26,16,20">
   <StackPanel DockPanel.Dock="Top" Margin="10,0,0,26"><TextBlock Text="澄桌" FontSize="24" FontWeight="SemiBold"/><TextBlock Text="让桌面，轻一点。" Foreground="#9299AA" Margin="0,8,0,0" FontSize="11"/></StackPanel>
   <TextBlock DockPanel.Dock="Bottom" Text="本地安装版" Foreground="#72798A" Margin="10,0,0,0" FontSize="11"/>
   <ListBox Name="SettingsNav" SelectedIndex="0" Background="Transparent" BorderThickness="0" Foreground="White" VerticalAlignment="Top">
    <ListBox.Resources><Style TargetType="ListBoxItem"><Setter Property="Margin" Value="0,3"/><Setter Property="Padding" Value="13,12"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem"><Border Name="NavSurface" CornerRadius="9" Background="Transparent" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="NavSurface" Property="Background" Value="#12FFFFFF"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="NavSurface" Property="Background" Value="#CC346DCA"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style></ListBox.Resources>
    <ListBoxItem><StackPanel Orientation="Horizontal"><TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE790;" Foreground="#ADBDFF" Width="26"/><TextBlock Text="外观"/></StackPanel></ListBoxItem><ListBoxItem><StackPanel Orientation="Horizontal"><TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE90A;" Foreground="#A4DAEF" Width="26"/><TextBlock Text="Dock"/></StackPanel></ListBoxItem><ListBoxItem><StackPanel Orientation="Horizontal"><TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE8F1;" Foreground="#C7B4FF" Width="26"/><TextBlock Text="应用与分类"/></StackPanel></ListBoxItem><ListBoxItem><StackPanel Orientation="Horizontal"><TextBlock FontFamily="Segoe MDL2 Assets" Text="&#xE713;" Foreground="#C0C4CD" Width="26"/><TextBlock Text="通用"/></StackPanel></ListBoxItem>
   </ListBox>
  </DockPanel></Border>
  <Grid Grid.Column="1" Margin="30,28,30,22"><Grid.RowDefinitions><RowDefinition Height="*"/><RowDefinition Height="Auto"/></Grid.RowDefinitions>
   <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="0,0,8,0"><Grid>
    <StackPanel Name="LookPanel"><TextBlock Text="让桌面，轻一点。" FontSize="24" FontWeight="SemiBold"/><TextBlock Text="柔和材质，留给专注更多空间。" Style="{StaticResource Hint}" Margin="0,8,0,4"/>
     <Border Style="{StaticResource Card}"><StackPanel><TextBlock Text="玻璃透明度"/><Slider Name="Glass" Minimum="20" Maximum="80" Value="55"/><TextBlock Text="向右更透明，实时应用到 Dock 和启动台。" Style="{StaticResource Hint}" FontSize="12"/></StackPanel></Border>
     <Border Style="{StaticResource Card}"><StackPanel><TextBlock Text="桌面壁纸"/><TextBlock Text="沿用现有 Wallpaper 壁纸" Margin="0,12,0,4" Foreground="#DCE4F8"/><TextBlock Text="壁纸与原有开机启动设置继续由 Wallpaper 管理。" Style="{StaticResource Hint}" FontSize="12"/></StackPanel></Border>
    </StackPanel>
    <StackPanel Name="DockPanel" Visibility="Collapsed"><TextBlock Text="常用应用，触手可及。" FontSize="24" FontWeight="SemiBold"/><TextBlock Text="轻盈图标，熟悉的打开与切换方式。" Style="{StaticResource Hint}" Margin="0,8,0,4"/>
     <Border Style="{StaticResource Card}"><StackPanel><TextBlock Text="应用图标大小"/><Slider Name="Size" Minimum="28" Maximum="48" Value="32"/><TextBlock Text="28–48 px · 悬停放大与点击反馈随图标调整。" Style="{StaticResource Hint}" FontSize="12"/></StackPanel></Border>
     <Border Style="{StaticResource Card}"><StackPanel><TextBlock Text="原生应用图标"/><TextBlock Text="保留应用原有图标，隐藏快捷方式箭头。" Style="{StaticResource Hint}" Margin="0,10,0,0"/><TextBlock Text="窗口覆盖 Dock 时，Dock 保持在下层。" Style="{StaticResource Hint}" Margin="0,8,0,0"/></StackPanel></Border>
    </StackPanel>
    <StackPanel Name="AppsPanel" Visibility="Collapsed">
     <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><StackPanel><TextBlock Text="你的应用，随手可达。" FontSize="24"/><TextBlock Text="搜索、添加与分类，在一处完成。" Style="{StaticResource Hint}" Margin="0,8,0,0"/></StackPanel><Button Name="AddApp" Grid.Column="1" Content="＋ 添加应用" VerticalAlignment="Center" Background="#80497BC3" Margin="12,0,0,0"/></Grid>
     <TextBlock Name="AppFeedback" Text="可一次选择多个 .exe 或 .lnk 文件，自动识别图标与分类。" Style="{StaticResource Hint}" FontSize="12" Margin="0,16,0,0"/>
     <Border Style="{StaticResource Card}" Padding="16"><StackPanel>
      <DockPanel Margin="0,0,0,10"><TextBlock Text="应用库" FontSize="15" DockPanel.Dock="Left"/><TextBlock Name="AppCount" HorizontalAlignment="Right" Foreground="#9299AA"/></DockPanel>
      <TextBox Name="AppSearch" ToolTip="按应用名称搜索" Margin="0,0,0,10"/>
      <TextBlock Text="输入名称筛选应用" Foreground="#9299AA" FontSize="11" Margin="2,0,0,10"/>
      <ListBox Name="AppChoice" Height="202" Background="Transparent" Foreground="White" BorderThickness="0" ScrollViewer.HorizontalScrollBarVisibility="Disabled" ScrollViewer.VerticalScrollBarVisibility="Auto">
       <ListBox.Resources><Style TargetType="ListBoxItem"><Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Padding" Value="10,8"/><Setter Property="Margin" Value="0,2"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem"><Border Name="Row" Background="Transparent" CornerRadius="9" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Row" Property="Background" Value="#10FFFFFF"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter TargetName="Row" Property="Background" Value="#40497BC3"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style></ListBox.Resources>
       <ListBox.ItemTemplate><DataTemplate><Grid><Grid.ColumnDefinitions><ColumnDefinition Width="42"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><Image Source="{Binding Icon}" Width="28" Height="28" Margin="0,0,12,0"/><TextBlock Grid.Column="1" Text="{Binding Name}" TextTrimming="CharacterEllipsis" VerticalAlignment="Center" Margin="0,0,10,0"/><Border Grid.Column="2" CornerRadius="5" Background="#14FFFFFF" Padding="8,3" VerticalAlignment="Center"><TextBlock Text="{Binding Category}" Foreground="#B8C2D8" FontSize="11"/></Border></Grid></DataTemplate></ListBox.ItemTemplate>
      </ListBox>
      <TextBlock Name="AppEmpty" Text="没有匹配的应用，试试其他名称或添加应用。" Style="{StaticResource Hint}" Visibility="Collapsed" Margin="0,10,0,8"/>
      <Border BorderBrush="#16FFFFFF" BorderThickness="0,1,0,0" Margin="0,14,0,0" Padding="0,14,0,0"><StackPanel>
       <TextBlock Name="SelectedAppName" Text="选择一个应用" FontSize="14"/><TextBlock Name="SelectedAppPath" Foreground="#9299AA" FontSize="11" TextTrimming="CharacterEllipsis" Margin="0,6,0,8"/>
       <Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><ComboBox Name="CategoryChoice" Margin="0,0,10,0"/><Button Name="MoveApp" Grid.Column="1" Content="保存分类" Margin="0"/></Grid>
      </StackPanel></Border>
     </StackPanel></Border>
     <Border Style="{StaticResource Card}" Padding="16"><StackPanel><TextBlock Text="自定义分类" FontSize="14"/><Grid Margin="0,12,0,0"><Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions><TextBox Name="NewCategory" MaxLength="12" ToolTip="分类名称，最多 12 个字" Margin="0,0,10,0"/><Button Name="CreateCategory" Grid.Column="1" Content="添加分类" Margin="0"/></Grid></StackPanel></Border>
    </StackPanel>
    <StackPanel Name="GeneralPanel" Visibility="Collapsed"><TextBlock Text="按你的习惯运行。" FontSize="24" FontWeight="SemiBold"/><TextBlock Text="启动与退出，都由你掌握。" Style="{StaticResource Hint}" Margin="0,8,0,4"/>
     <Border Style="{StaticResource Card}"><StackPanel><CheckBox Name="AutoStart" Content="登录 Windows 后自动启动" Foreground="White" Padding="6"/><TextBlock Text="登录后只显示 Dock，不主动展开设置或启动台。" Style="{StaticResource Hint}" Margin="0,12,0,0" FontSize="12"/></StackPanel></Border>
     <Border Style="{StaticResource Card}"><StackPanel><TextBlock Text="窗口动画"/><TextBlock Name="AnimationStatus" Text="动画状态" Style="{StaticResource Hint}" Margin="0,12,0,0"/><TextBlock Text="部分应用与多屏场景仍有兼容性限制。" Style="{StaticResource Hint}" Margin="0,10,0,0" FontSize="12"/></StackPanel></Border>
     <Button Name="Quit" Content="退出澄桌" HorizontalAlignment="Left" Margin="0,22,0,0" Foreground="#FFC4C4"/>
    </StackPanel>
   </Grid></ScrollViewer>
   <Border Grid.Row="1" BorderBrush="#18FFFFFF" BorderThickness="0,1,0,0" Margin="0,20,0,0" Padding="0,14,0,0"><TextBlock Text="更改自动保存" Foreground="#9299AA" FontSize="11"/></Border>
  </Grid>
 </Grid>
</Window>
'@
$global:cd_visualResources=Read-Xaml @'
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
 <Style TargetType="TextBox"><Setter Property="Foreground" Value="#F4F4F7"/><Setter Property="CaretBrush" Value="White"/><Setter Property="Background" Value="#16FFFFFF"/><Setter Property="BorderBrush" Value="#24FFFFFF"/><Setter Property="Padding" Value="12,8"/><Setter Property="SelectionBrush" Value="#806AA8FF"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="TextBox"><Border Name="Entry" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="8" Padding="{TemplateBinding Padding}"><ScrollViewer Name="PART_ContentHost"/></Border><ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Entry" Property="BorderBrush" Value="#8E91BBF5"/></Trigger><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Entry" Property="Background" Value="#20FFFFFF"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
 <Style TargetType="ToolTip"><Setter Property="Background" Value="#F035363B"/><Setter Property="Foreground" Value="White"/><Setter Property="BorderThickness" Value="0"/><Setter Property="Padding" Value="11,6"/><Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ToolTip"><Border Background="{TemplateBinding Background}" CornerRadius="7" Padding="{TemplateBinding Padding}" BorderBrush="#35FFFFFF" BorderThickness="1"><ContentPresenter/></Border></ControlTemplate></Setter.Value></Setter></Style>
</ResourceDictionary>
'@
foreach($window in @($global:cd_dock,$global:cd_launcher,$global:cd_settings)){
 $window.Resources[ [Windows.Controls.TextBox] ]=$global:cd_visualResources[[Windows.Controls.TextBox]]
 $window.Resources[ [Windows.Controls.ToolTip] ]=$global:cd_visualResources[[Windows.Controls.ToolTip]]
 [Windows.Media.TextOptions]::SetTextFormattingMode($window,[Windows.Media.TextFormattingMode]::Display)
 [Windows.Media.TextOptions]::SetTextRenderingMode($window,[Windows.Media.TextRenderingMode]::ClearType)
 $window.UseLayoutRounding=$true;$window.SnapsToDevicePixels=$true
}
Trace-Stage 'Windows created'
function Find($window,[string]$name) { $window.FindName($name) }
$global:cd_buttonTemplate = Read-Xaml @'
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
 <Border Name="Surface" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="9" RenderTransformOrigin="0.5,0.5" Padding="{TemplateBinding Padding}">
  <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/>
  <Border.RenderTransform><ScaleTransform ScaleX="1" ScaleY="1"/></Border.RenderTransform>
 </Border>
 <ControlTemplate.Triggers>
  <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Surface" Property="Opacity" Value="0.92"/></Trigger>
  <Trigger Property="IsPressed" Value="True"><Setter TargetName="Surface" Property="Opacity" Value="0.78"/><Setter TargetName="Surface" Property="RenderTransform"><Setter.Value><ScaleTransform ScaleX="0.97" ScaleY="0.97"/></Setter.Value></Setter></Trigger>
  <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Surface" Property="BorderBrush" Value="#7FA9D5FF"/></Trigger>
  <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Surface" Property="Opacity" Value="0.35"/></Trigger>
 </ControlTemplate.Triggers>
</ControlTemplate>
'@
$global:cd_dockButtonTemplate = Read-Xaml @'
<ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button"><Border Background="Transparent" Padding="{TemplateBinding Padding}" ClipToBounds="False"><ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/></Border></ControlTemplate>
'@
function Set-ButtonTemplate($element) {
 if($element -is [Windows.Controls.Button]){if($element.Tag -eq 'DockButton'){$element.Template=$global:cd_dockButtonTemplate}else{$element.Template=$global:cd_buttonTemplate}}
 if($element -is [Windows.DependencyObject]){for($i=0;$i -lt [Windows.Media.VisualTreeHelper]::GetChildrenCount($element);$i++){Set-ButtonTemplate ([Windows.Media.VisualTreeHelper]::GetChild($element,$i))}}
}
function New-Button([string]$text) {
 $b=[Windows.Controls.Button]::new();$b.Template=$global:cd_buttonTemplate;$b.Content=$text;$b.Foreground=[Windows.Media.Brushes]::White;$b.Background=[Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromArgb(18,255,255,255));$b.BorderBrush=[Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromArgb(26,255,255,255));$b.Padding='12,6';$b.Margin='3';return $b
}
function New-FeatureButton([string]$name,[string]$glyph,[string]$top,[string]$bottom) {
 $button=New-Button '';$button.Tag='DockButton';$button.ToolTip=$name;$button.Margin='2';$button.Padding='4';$button.Background=[Windows.Media.Brushes]::Transparent;$button.BorderThickness='0'
 $stack=[Windows.Controls.StackPanel]::new()
 $tile=[Windows.Controls.Border]::new();$tile.Width=32;$tile.Height=32;$tile.CornerRadius='9';$tile.Tag='DockAppIcon';$tile.HorizontalAlignment='Center';$tile.Background=[Windows.Media.Brushes]::Transparent
 $symbol=[Windows.Controls.TextBlock]::new();$symbol.FontFamily=[Windows.Media.FontFamily]::new('Segoe MDL2 Assets');$symbol.Text=$glyph;$symbol.FontSize=26;$symbol.Foreground=[Windows.Media.Brushes]::White;$symbol.VerticalAlignment='Center';$symbol.HorizontalAlignment='Center';$tile.Child=$symbol;$stack.Children.Add($tile)|Out-Null
 $label=[Windows.Controls.TextBlock]::new();$label.Text=$name;$label.FontSize=10;$label.MaxWidth=72;$label.Margin='0,3,0,0';$label.HorizontalAlignment='Center';$stack.Children.Add($label)|Out-Null;$label.Visibility='Collapsed';$button.Content=$stack;return $button
}
function Open-App($app,$source=$null,[bool]$toggle=$false) {
 try {
  $executable=$app.Path
  if($app.Name -eq 'Codex'){

$package=[pscustomobject]@{InstallLocation='';PackageFamilyName='OpenAI.Codex_2p2nqsd0c76g0'}
$active=Get-Process ChatGPT -ErrorAction SilentlyContinue | Where-Object Path | Select-Object -First 1
if($active){$package.InstallLocation=Split-Path (Split-Path $active.Path -Parent) -Parent}
   if($package){
    $target=if($package.InstallLocation){Join-Path $package.InstallLocation 'app\ChatGPT.exe'}else{''}
    if($package.InstallLocation -and $source -and [ClearDesktopGenie]::Handle($target,$source,$false)){[ClearDesktopMotion]::HideLauncher($global:cd_launcher);return}
    Start-Process -FilePath (Join-Path $env:WINDIR 'explorer.exe') -ArgumentList ('shell:AppsFolder\'+$package.PackageFamilyName+'!App')
    [ClearDesktopMotion]::HideLauncher($global:cd_launcher);return
   }
  }
  if($executable -like '*.lnk') {
   $shell=New-Object -ComObject WScript.Shell
   try{$executable=$shell.CreateShortcut($app.Path).TargetPath}finally{[void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell)}
  }
  $executable=[Environment]::ExpandEnvironmentVariables($executable)
  if($source -and [IO.File]::Exists($executable) -and [ClearDesktopGenie]::Handle($executable,$source,$toggle)) {
   [ClearDesktopMotion]::HideLauncher($global:cd_launcher);return
  }
  Start-Process -FilePath $app.Path | Out-Null
  [ClearDesktopMotion]::HideLauncher($global:cd_launcher)
 } catch { [Windows.MessageBox]::Show('应用无法打开或切换，请检查快捷方式。','澄桌') | Out-Null }
}
function Get-AppIcon($app) {
 if ($app.Icon) { return $app.Icon }
 $handle=[IntPtr]::Zero
 try {
  $iconPath=$app.Path
  if($app.Path -like '*.lnk'){
   $shell=New-Object -ComObject WScript.Shell
   try{
    $shortcut=$shell.CreateShortcut($app.Path)
    $location=[Environment]::ExpandEnvironmentVariables([string]$shortcut.IconLocation)
    if($location -match '^(.+),\s*(-?\d+)$'){
     $resource=$matches[1].Trim('"');$index=[int]$matches[2]
     if(Test-Path -LiteralPath $resource){$handle=[ClearDesktopIcons]::LoadResource($resource,$index)}
    }
    $target=[Environment]::ExpandEnvironmentVariables([string]$shortcut.TargetPath)
    if($target -and (Test-Path -LiteralPath $target)){$iconPath=$target}
   }finally{[void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell)}
  }
  if($handle -eq [IntPtr]::Zero){$handle=[ClearDesktopIcons]::Load($iconPath)}
  if ($handle -ne [IntPtr]::Zero) {
   $app.Icon=[Windows.Interop.Imaging]::CreateBitmapSourceFromHIcon($handle,[Windows.Int32Rect]::Empty,[Windows.Media.Imaging.BitmapSizeOptions]::FromEmptyOptions());$app.Icon.Freeze()
  }
 } catch { } finally { if($handle -ne [IntPtr]::Zero){[ClearDesktopIcons]::DestroyIcon($handle)|Out-Null} }
 return $app.Icon
}
function Render-Apps {
 $panel=Find $global:cd_launcher 'Apps';$panel.Children.Clear();$query=(Find $global:cd_launcher 'Search').Text.Trim();$count=0
 $filtered=@($global:cd_apps | Sort-Object Name | Where-Object {($global:cd_category -eq '全部' -or $_.Category -eq $global:cd_category) -and (-not $query -or $_.Name.IndexOf($query,[StringComparison]::OrdinalIgnoreCase) -ge 0)})
 $count=$filtered.Count
 $cols=[Math]::Max(1,[Math]::Floor(($global:cd_launcher.Width-54)/108));$rows=[Math]::Max(1,[Math]::Floor(($global:cd_launcher.Height-240)/98));$pageSize=[int]($cols*$rows)
 $panel.Columns=$cols;$panel.Rows=$rows
 $global:cd_totalPages=[Math]::Max(1,[Math]::Ceiling($count/$pageSize))
 $global:cd_page=[Math]::Max(0,[Math]::Min($global:cd_page,$global:cd_totalPages-1))
 (Find $global:cd_launcher 'PageNumber').Text=([string]($global:cd_page+1))+' / '+$global:cd_totalPages
 (Find $global:cd_launcher 'PrevPage').IsEnabled=$global:cd_page -gt 0
 (Find $global:cd_launcher 'NextPage').IsEnabled=$global:cd_page -lt ($global:cd_totalPages-1)
 foreach ($app in ($filtered | Select-Object -Skip ([int]($global:cd_page*$pageSize)) -First $pageSize)) {
  $b=New-Button '';$b.Width=100;$b.Height=96;$b.Background=[Windows.Media.Brushes]::Transparent;$b.BorderThickness='0';$b.ToolTip=$app.Name
  $stack=[Windows.Controls.StackPanel]::new();$stack.HorizontalAlignment='Center';$ico=Get-AppIcon $app
  if ($ico) { $image=[Windows.Controls.Image]::new();$image.Source=$ico;$image.Width=44;$image.Height=44;$image.Margin='0,0,0,10';$stack.Children.Add($image)|Out-Null }
  else { $symbol=[Windows.Controls.TextBlock]::new();$symbol.Text='▣';$symbol.FontSize=26;$symbol.HorizontalAlignment='Center';$stack.Children.Add($symbol)|Out-Null }
  $label=[Windows.Controls.TextBlock]::new();$label.Text=$app.Name;$label.FontSize=11;$label.MaxWidth=88;$label.TextTrimming='CharacterEllipsis';$label.HorizontalAlignment='Center';$stack.Children.Add($label)|Out-Null;$b.Content=$stack
  $chosen=$app;$b.Add_Click({ param($sender,$eventArgs) Open-App $chosen $sender $false }.GetNewClosure());$panel.Children.Add($b)|Out-Null
 }
 (Find $global:cd_launcher 'Results').Text=if($query){'找到 '+$count+' 个应用'}elseif($count -eq 0){'此分类暂无应用'}else{''}
}
function Render-Categories {
 $bar=Find $global:cd_launcher 'Categories';$bar.Children.Clear()
 foreach ($cat in $global:cd_categories) { $b=New-Button $cat;if($cat -eq $global:cd_category){$b.Background=[Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromArgb(55,255,255,255))};$chosen=$cat;$b.Add_Click({$global:cd_category=$chosen;$global:cd_page=0;Render-Categories;Render-Apps}.GetNewClosure());$bar.Children.Add($b)|Out-Null }
}
function Toggle-Launcher {
 if ($global:cd_lastHidden -and ((Get-Date)-$global:cd_lastHidden).TotalMilliseconds -lt 250) { return }
 if ($global:cd_launcher.IsVisible) { [ClearDesktopMotion]::HideLauncher($global:cd_launcher);return }
 Render-Categories;Render-Apps;[ClearDesktopMotion]::ShowLauncher($global:cd_launcher);Set-ButtonTemplate $global:cd_launcher;$global:cd_launcher.Activate()|Out-Null;(Find $global:cd_launcher 'Search').Focus()|Out-Null
}
function Refresh-Settings {
 $choice=Find $global:cd_settings 'AppChoice';$previous=$choice.SelectedItem.Path
 $query=(Find $global:cd_settings 'AppSearch').Text.Trim()
 $filtered=@($global:cd_apps | Sort-Object Name | Where-Object {-not $query -or $_.Name.IndexOf($query,[StringComparison]::OrdinalIgnoreCase) -ge 0})
 foreach($app in $filtered){[void](Get-AppIcon $app)}
 $choice.ItemsSource=$null;$choice.ItemsSource=$filtered
 $selected=$filtered | Where-Object Path -eq $previous | Select-Object -First 1
 if($selected){$choice.SelectedItem=$selected}elseif($filtered.Count){$choice.SelectedIndex=0}
 (Find $global:cd_settings 'AppCount').Text=([string]$filtered.Count)+' / '+$global:cd_apps.Count+' 个应用'
 (Find $global:cd_settings 'AppEmpty').Visibility=if($filtered.Count){'Collapsed'}else{'Visible'}
 $categoryChoice=Find $global:cd_settings 'CategoryChoice';$categoryChoice.ItemsSource=@($global:cd_categories | Where-Object {$_ -ne '全部'})
 Update-AppSelection
}
function Update-AppSelection {
 $app=(Find $global:cd_settings 'AppChoice').SelectedItem
 (Find $global:cd_settings 'MoveApp').IsEnabled=$null -ne $app
 (Find $global:cd_settings 'CategoryChoice').IsEnabled=$null -ne $app
 if($app){
  (Find $global:cd_settings 'SelectedAppName').Text=$app.Name
  (Find $global:cd_settings 'SelectedAppPath').Text=$app.Path
  (Find $global:cd_settings 'SelectedAppPath').ToolTip=$app.Path
  (Find $global:cd_settings 'CategoryChoice').SelectedItem=$app.Category
 }else{
  (Find $global:cd_settings 'SelectedAppName').Text='选择一个应用'
  (Find $global:cd_settings 'SelectedAppPath').Text=''
 }
}
function Show-Settings { Refresh-Settings;[ClearDesktopMotion]::HideLauncher($global:cd_launcher);$global:cd_settings.Show();Set-ButtonTemplate $global:cd_settings;$global:cd_settings.Activate()|Out-Null }
$items=Find $global:cd_dock 'DockItems'
$button=New-FeatureButton '应用' ([string][char]0xE80A) '#68A3F1' '#3267B7';$button.Add_Click({Toggle-Launcher});$items.Children.Add($button)|Out-Null
$button=New-FeatureButton '设置' ([string][char]0xE713) '#919AA7' '#515B69';$button.Add_Click({Show-Settings});$items.Children.Add($button)|Out-Null
$separator=[Windows.Controls.Border]::new();$separator.Width=1;$separator.Height=27;$separator.Margin='8,0,8,0';$separator.Background=[Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromArgb(42,255,255,255));$items.Children.Add($separator)|Out-Null
$global:cd_pinned=@($global:cd_apps | Where-Object {$_.Name -match '资源管理器|Microsoft Edge|Google Chrome|微信|WeChat|Codex'} | Select-Object -First 5)
if($global:cd_saved -and $null -ne $global:cd_saved.PinnedPaths){
 $global:cd_pinned=@(foreach($path in $global:cd_saved.PinnedPaths){$global:cd_apps | Where-Object Path -eq $path | Select-Object -First 1})
}
foreach ($app in $global:cd_pinned) {
 $button=New-Button '';$button.Tag='DockButton';$button.ToolTip=$app.Name;$button.Background=[Windows.Media.Brushes]::Transparent;$button.BorderThickness='0';$button.Padding='4';$button.Margin='2';$stack=[Windows.Controls.StackPanel]::new();$icon=Get-AppIcon $app
 if($icon){$image=[Windows.Controls.Image]::new();$image.Source=$icon;$image.Width=32;$image.Height=32;$image.Tag='DockAppIcon';$stack.Children.Add($image)|Out-Null}else{$text=[Windows.Controls.TextBlock]::new();$text.Text='▣';$text.FontSize=30;$stack.Children.Add($text)|Out-Null}
 $label=[Windows.Controls.TextBlock]::new();$label.Text=$app.Name;$label.FontSize=10;$label.MaxWidth=72;$label.TextTrimming='CharacterEllipsis';$label.HorizontalAlignment='Center';$label.Visibility='Collapsed';$stack.Children.Add($label)|Out-Null;$button.Content=$stack
 try{$exe=$app.Path;if($exe -like '*.lnk'){$shell=New-Object -ComObject WScript.Shell;try{$exe=$shell.CreateShortcut($exe).TargetPath}finally{[void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell)}};[ClearDesktopGenie]::Register([Environment]::ExpandEnvironmentVariables($exe),$button)}catch{}
 $chosen=$app;$button.Add_Click({param($sender,$eventArgs) Open-App $chosen $sender $true}.GetNewClosure());$items.Children.Add($button)|Out-Null
}

$work=[Windows.SystemParameters]::WorkArea
$global:cd_launcher.Width=[Math]::Min(880,$work.Width-50);$global:cd_launcher.Height=[Math]::Min(570,$work.Height-150);$global:cd_launcher.Left=$work.Left+($work.Width-$global:cd_launcher.Width)/2;$global:cd_launcher.Top=[Math]::Min($work.Top+($work.Height-$global:cd_launcher.Height)/2,$work.Bottom-$global:cd_launcher.Height-90)
$global:cd_dock.Add_SourceInitialized({$handle=([Windows.Interop.WindowInteropHelper]::new($global:cd_dock)).Handle;[ClearDesktopDockLayer]::Configure($handle)})
$global:cd_dock.Add_ContentRendered({$work=[Windows.SystemParameters]::WorkArea;$global:cd_dock.Left=$work.Left+($work.Width-$global:cd_dock.ActualWidth)/2;$global:cd_dock.Top=$work.Bottom-$global:cd_dock.ActualHeight-12;[ClearDesktopDockLayer]::Refresh()})
$global:cd_launcher.Add_Deactivated({$global:cd_lastHidden=Get-Date;[ClearDesktopMotion]::HideLauncher($global:cd_launcher)})
$global:cd_launcher.Add_PreviewKeyDown({if($_.Key -eq 'Escape'){[ClearDesktopMotion]::HideLauncher($global:cd_launcher);$_.Handled=$true}})
(Find $global:cd_launcher 'Search').Add_TextChanged({$global:cd_category='全部';$global:cd_page=0;Render-Categories;Render-Apps})
(Find $global:cd_launcher 'SearchButton').Add_Click({$global:cd_page=0;Render-Apps})
(Find $global:cd_launcher 'PrevPage').Add_Click({if($global:cd_page -gt 0){$global:cd_page--;Render-Apps}})
(Find $global:cd_launcher 'NextPage').Add_Click({if($global:cd_page -lt ($global:cd_totalPages-1)){$global:cd_page++;Render-Apps}})

(Find $global:cd_settings 'SettingsNav').Add_SelectionChanged({
 $names=@('LookPanel','DockPanel','AppsPanel','GeneralPanel');$selected=(Find $global:cd_settings 'SettingsNav').SelectedIndex
 for($i=0;$i -lt $names.Count;$i++){(Find $global:cd_settings $names[$i]).Visibility=if($i -eq $selected){'Visible'}else{'Collapsed'}}
})
$global:cd_settings.Add_Closing({if(-not $global:cd_quitting){$_.Cancel=$true;$global:cd_settings.Hide()}})
(Find $global:cd_settings 'Glass').Add_ValueChanged({$alpha=[byte](255*(1-$_.NewValue/100));$global:cd_dock.Content.Background=[Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromArgb($alpha,48,50,55));$global:cd_launcher.Content.Background=[Windows.Media.SolidColorBrush]::new([Windows.Media.Color]::FromArgb($alpha,32,34,38))})
(Find $global:cd_settings 'Size').Add_ValueChanged({foreach($b in (Find $global:cd_dock 'DockItems').Children){if($b.Content -is [Windows.Controls.StackPanel]){foreach($child in $b.Content.Children){if($child -is [Windows.Controls.Image] -or $child.Tag -eq 'DockAppIcon'){$child.Width=$_.NewValue;$child.Height=$_.NewValue}}}}})
(Find $global:cd_settings 'AppSearch').Add_TextChanged({Refresh-Settings})
(Find $global:cd_settings 'AppChoice').Add_SelectionChanged({Update-AppSelection})
(Find $global:cd_settings 'AddApp').Add_Click({
 $dialog=[Microsoft.Win32.OpenFileDialog]::new();$dialog.Title='添加应用到澄桌';$dialog.Filter='应用或快捷方式|*.exe;*.lnk';$dialog.Multiselect=$true
 if($dialog.ShowDialog($global:cd_settings)){
  $added=0;$duplicate=0
  foreach($file in $dialog.FileNames){
   if($global:cd_apps | Where-Object Path -eq $file){$duplicate++;continue}
   $name=[IO.Path]::GetFileNameWithoutExtension($file)
   $app=[pscustomobject]@{Name=$name;Path=$file;Category=(Get-Category $name);Icon=$null}
   $global:cd_apps.Add($app);$added++
  }
  (Find $global:cd_settings 'AppSearch').Clear();Refresh-Settings;Save-Settings
  if($added){(Find $global:cd_settings 'AppChoice').SelectedItem=$app}
  (Find $global:cd_settings 'AppFeedback').Text='已添加 '+$added+' 个应用'+$(if($duplicate){'，跳过 '+$duplicate+' 个重复入口。'}else{'，可在启动台搜索并打开。'})
 }
})
(Find $global:cd_settings 'MoveApp').Add_Click({
 $app=(Find $global:cd_settings 'AppChoice').SelectedItem;$cat=(Find $global:cd_settings 'CategoryChoice').SelectedItem
 if($app -and $cat){$app.Category=[string]$cat;Save-Settings;Refresh-Settings;(Find $global:cd_settings 'AppFeedback').Text='已将“'+$app.Name+'”归入“'+$cat+'”。'}
})
(Find $global:cd_settings 'CreateCategory').Add_Click({
 $name=(Find $global:cd_settings 'NewCategory').Text.Trim()
 if(-not $name){(Find $global:cd_settings 'AppFeedback').Text='请先输入分类名称。';return}
 if($global:cd_categories.Contains($name)){(Find $global:cd_settings 'AppFeedback').Text='该分类已存在。';return}
 if($name.Length -gt 12){(Find $global:cd_settings 'AppFeedback').Text='分类名称最多 12 个字。';return}
 $global:cd_categories.Add($name);(Find $global:cd_settings 'NewCategory').Clear();Refresh-Settings;Save-Settings
 (Find $global:cd_settings 'CategoryChoice').SelectedItem=$name
 (Find $global:cd_settings 'AppFeedback').Text='已新增“'+$name+'”，可为选中的应用保存此分类。'
})
(Find $global:cd_settings 'Quit').Add_Click({Save-Settings;$global:cd_quitting=$true;[ClearDesktopGenie]::Shutdown();$global:cd_context.ExitThread();$global:cd_dock.Close();$global:cd_launcher.Close();$global:cd_settings.Close()})
$global:cd_animationStatusTimer=[Windows.Threading.DispatcherTimer]::new();$global:cd_animationStatusTimer.Interval=[TimeSpan]::FromSeconds(1);$global:cd_animationStatusTimer.Add_Tick({if($global:cd_settings.IsVisible){(Find $global:cd_settings 'AnimationStatus').Text='动画状态：'+[ClearDesktopGenie]::LastOutcome}});$global:cd_animationStatusTimer.Start()
$global:cd_context=[Windows.Forms.ApplicationContext]::new()
if($global:cd_saved){
 if($null -ne $global:cd_saved.Glass){(Find $global:cd_settings 'Glass').Value=[Math]::Max(20,[Math]::Min(80,[double]$global:cd_saved.Glass))}
 if($null -ne $global:cd_saved.Size){(Find $global:cd_settings 'Size').Value=[Math]::Max(28,[Math]::Min(48,[double]$global:cd_saved.Size))}
}
(Find $global:cd_settings 'Glass').Add_ValueChanged({Save-Settings})
(Find $global:cd_settings 'Size').Add_ValueChanged({Save-Settings})
# Persist only user changes; never replace saved data during startup.
$global:cd_startupLink=Join-Path ([Environment]::GetFolderPath('Startup')) '澄桌.lnk'
$global:cd_installedExe=Join-Path $global:cd_root 'ClearDesktop.exe'
(Find $global:cd_settings 'AutoStart').IsChecked=Test-Path -LiteralPath $global:cd_startupLink
(Find $global:cd_settings 'AutoStart').Add_Click({
 try{
  if((Find $global:cd_settings 'AutoStart').IsChecked){
   $shell=New-Object -ComObject WScript.Shell
   try{$link=$shell.CreateShortcut($global:cd_startupLink);$link.TargetPath=$global:cd_installedExe;$link.WorkingDirectory=$global:cd_root;$link.Description='澄桌桌面工具';$link.Save()}finally{[void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell)}
  }else{if(Test-Path -LiteralPath $global:cd_startupLink){Remove-Item -LiteralPath $global:cd_startupLink}}
 }catch{(Find $global:cd_settings 'AutoStart').IsChecked=Test-Path -LiteralPath $global:cd_startupLink;[Windows.MessageBox]::Show('无法更新开机启动：'+$_.Exception.Message,'澄桌')|Out-Null}
})
Trace-Stage 'Showing dock'
$global:cd_dock.Show();Set-ButtonTemplate $global:cd_dock;[ClearDesktopGenie]::Start((Find $global:cd_dock 'DockItems').Children[0]);[ClearDesktopMotion]::AttachDock((Find $global:cd_dock 'DockItems'));Trace-Stage 'Running dispatcher'
[Windows.Forms.Application]::Run($global:cd_context)
