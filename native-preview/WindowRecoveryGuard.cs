using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
class WindowRecoveryGuard {
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attr,ref int value,int size);
 static bool Alive(int pid,long start){try{using(var p=Process.GetProcessById(pid))return !p.HasExited&&p.StartTime.ToUniversalTime().Ticks==start;}catch{return false;}}
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
 [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr h,uint key,byte alpha,uint flags);
 [STAThread] static void Main(string[] args){
  if(args.Length!=3)return;int owner;long start;if(!Int32.TryParse(args[0],out owner)||!Int64.TryParse(args[2],out start))return;
  string file=args[1];bool failed=false;
  while(true){bool alive=Alive(owner,start);try{if(File.Exists(file))foreach(var line in File.ReadAllLines(file)){
   var parts=line.Split('|');if(parts.Length!=9)continue;long handle,deadline;int pid,original,cloak;
   if(!Int64.TryParse(parts[0],out handle)||!Int32.TryParse(parts[1],out pid)||!Int32.TryParse(parts[2],out original)||!Int32.TryParse(parts[3],out cloak)||!Int64.TryParse(parts[4],out deadline))continue;
   var window=new IntPtr(handle);uint actual;if(!IsWindow(window))continue;GetWindowThreadProcessId(window,out actual);if(actual!=(uint)pid)continue;
   if(cloak!=0&&(!alive||DateTime.UtcNow.Ticks>deadline)){
    long style;byte alpha;uint key,flags;
    if(!Int64.TryParse(parts[5],out style)||!Byte.TryParse(parts[6],out alpha)||!UInt32.TryParse(parts[7],out key)||!UInt32.TryParse(parts[8],out flags))continue;
    if(!SetLayeredWindowAttributes(window,key,alpha,flags))failed=true;
    if((style&0x80000)==0)SetWindowLongPtr(window,-20,new IntPtr(GetWindowLongPtr(window,-20).ToInt64()&~0x80000L));
   }
   if(!alive&&DwmSetWindowAttribute(window,3,ref original,4)!=0)failed=true;
  }}catch{}
  if(!alive){try{File.Delete(file);}catch{}Environment.ExitCode=failed?1:0;return;}Thread.Sleep(100);
  }
 }
}
