using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Setup {
 static readonly string[] Files={"installed/ClearDesktop.exe","installed/WindowRecoveryGuard.exe","installed/Start-ClearDesktop.ps1","installed/NativeIcons.cs","installed/NativeMotion.cs","installed/NativeGenie.cs","README.md","LICENSE","VERSION.txt"};
 static string InstallRoot {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"ClearDesktop");}}
 static void Extract(string root){
  root=Path.GetFullPath(root);string prefix=root.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("ClearDesktop.Payload")){
   if(stream==null)throw new IOException("安装包缺少程序文件。");
   using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)){
    var allowed=new HashSet<string>(Files,StringComparer.Ordinal);var found=new HashSet<string>(StringComparer.Ordinal);
    foreach(var entry in zip.Entries){
     if(!allowed.Contains(entry.FullName)||!found.Add(entry.FullName))throw new IOException("安装包文件清单不正确。");
     string path=Path.GetFullPath(Path.Combine(root,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));
     if(!path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("安装包路径不正确。");
    }
    if(found.Count!=Files.Length)throw new IOException("安装包不完整。");
    foreach(var entry in zip.Entries){
     string path=Path.Combine(root,entry.FullName.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(path));
     string temp=path+".installing";
     try{using(var input=entry.Open())using(var output=File.Create(temp))input.CopyTo(output);File.Copy(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
   }
  }
 }
 static void Shortcut(string path,string target){
  object shell=null,link=null;
  try{
   shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
   link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
   var type=link.GetType();
   type.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{target});
   type.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Path.GetDirectoryName(target)});
   type.InvokeMember("Description",BindingFlags.SetProperty,null,link,new object[]{"澄桌 · 应用启动台与 Dock"});
   type.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
  }finally{if(link!=null)Marshal.ReleaseComObject(link);if(shell!=null)Marshal.ReleaseComObject(shell);}
 }
 static bool IsRunning(){Mutex mutex;bool exists=Mutex.TryOpenExisting("Local\\ClearDesktopInstalled",out mutex);if(mutex!=null)mutex.Dispose();return exists;}
 [STAThread] static int Main(string[] args){
  if(args.Length==1&&args[0]=="--verify"){
   string tempRoot=Path.GetFullPath(Path.GetTempPath());string check=Path.Combine(tempRoot,"ClearDesktop-verify-"+Guid.NewGuid().ToString("N"));
   try{Extract(check);foreach(string file in Files)if(new FileInfo(Path.Combine(check,file)).Length==0)throw new IOException("Empty payload");return 0;}
   catch{return 1;}
   finally{if(check.StartsWith(tempRoot,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(check).StartsWith("ClearDesktop-verify-",StringComparison.Ordinal)&&Directory.Exists(check))Directory.Delete(check,true);}
  }
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new Form{Text="澄桌安装",ClientSize=new Size(540,290),StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,Font=new Font("Microsoft YaHei UI",10)}){
   form.Controls.Add(new Label{Text="澄桌 Clear Desktop",Font=new Font("Microsoft YaHei UI",20,FontStyle.Bold),AutoSize=true,Location=new Point(26,24)});
   form.Controls.Add(new Label{Text="为 Windows 添加轻盈的 Dock 与分类启动台。\n实验性预览版 · 无需管理员权限 · 无需下载额外组件",AutoSize=true,Location=new Point(28,82)});
   form.Controls.Add(new Label{Text="安装位置："+InstallRoot+"\n保留已有配置；开机自启动可在设置中选择。",AutoSize=false,Size=new Size(480,60),Location=new Point(28,142)});
   var button=new Button{Text="安装并启动",Size=new Size(160,42),Location=new Point(350,220)};form.Controls.Add(button);form.AcceptButton=button;
   button.Click+=(s,e)=>{
    button.Enabled=false;
    try{
     if(!Environment.Is64BitOperatingSystem)throw new InvalidOperationException("目前安装包仅支持 64 位 Windows。");
     if(IsRunning())throw new InvalidOperationException("请先在澄桌设置中退出正在运行的澄桌，再点击安装。");
     Extract(InstallRoot);string exe=Path.Combine(InstallRoot,"installed","ClearDesktop.exe");
     Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"澄桌.lnk"),exe);
     Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"澄桌.lnk"),exe);
     Process.Start(new ProcessStartInfo(exe){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exe),CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
     MessageBox.Show(form,"安装完成。底部 Dock 已启动，点击左侧齿轮可打开设置。","澄桌",MessageBoxButtons.OK,MessageBoxIcon.Information);form.Close();
    }catch(Exception ex){MessageBox.Show(form,ex.Message,"未完成安装",MessageBoxButtons.OK,MessageBoxIcon.Warning);button.Enabled=true;}
   };
   Application.Run(form);
  }
  return 0;
 }
}
