using System;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;
class PreviewHost {
 [STAThread] static void Main(string[] args) {
  bool created; using(var singleton=new Mutex(true,"Local\\ClearDesktopInstalled",out created)) { if(!created)return; string root=AppDomain.CurrentDomain.BaseDirectory;
  try {
   using (Runspace runspace=RunspaceFactory.CreateRunspace()) {
    runspace.ApartmentState=ApartmentState.STA;
    runspace.ThreadOptions=PSThreadOptions.UseCurrentThread;
    runspace.Open();
    using (PowerShell ps=PowerShell.Create()) {
     ps.Runspace=runspace;
     string script=File.ReadAllText(Path.Combine(root,args.Length>0 && args[0]=="--integration-test" ? "Test-Integration.ps1" : args.Length>0 && args[0]=="--genie-test" ? "Test-Genie.ps1" : "Start-ClearDesktop.ps1"));
     ps.AddScript(script.Replace("$PSScriptRoot", "'"+root.Replace("'","''")+"'"));
     ps.Invoke();
     if(ps.HadErrors) File.WriteAllText(Path.Combine(root,"host-error.log"),String.Join(Environment.NewLine,ps.Streams.Error));
    }
   }
  } catch(Exception ex) {File.WriteAllText(Path.Combine(root,"host-error.log"),ex.ToString());} }
 }
}
