using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

static class DockLayerTests {
 delegate bool EnumProc(IntPtr window,IntPtr value);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr value);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int msg,IntPtr wParam,IntPtr lParam);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int size);
 [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l,int t,int r,int b);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr region);
 [DllImport("gdi32.dll")] static extern bool PtInRegion(IntPtr region,int x,int y);
 [DllImport("user32.dll")] static extern int GetWindowRgn(IntPtr window,IntPtr region);
 static bool PointVisible(IntPtr dock,int x=50,int y=50){IntPtr region=CreateRectRgn(0,0,0,0);try{return GetWindowRgn(dock,region)==0||PtInRegion(region,x,y);}finally{DeleteObject(region);}}
 static bool Unobscured(IntPtr app,IntPtr dock){return Index(dock)>Index(app)||!PointVisible(dock);}
 static Window NewWindow(string name){return new Window{Title=name,Left=-18000,Top=-18000,Width=240,Height=160,ShowActivated=false,ShowInTaskbar=false,Background=Brushes.Gray};}
 static IntPtr Handle(Window window){return new WindowInteropHelper(window).Handle;}
 static void Pump(){Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(()=>{}));}
 static int Index(IntPtr window){int index=0,found=-1;EnumWindows((h,v)=>{if(h==window)found=index;index++;return true;},IntPtr.Zero);return found;}
 static void Check(string name,bool passed){Console.WriteLine((passed?"PASS ":"FAIL ")+name);if(!passed)throw new Exception(name);}
 [STAThread] static int Main(){
  var windows=new List<Window>();
  try{
   var first=NewWindow("Dock layer test: first app");windows.Add(first);first.Show();
   var second=NewWindow("Dock layer test: second app");windows.Add(second);second.Show();
   var dock=NewWindow("Dock layer test: Dock");windows.Add(dock);dock.WindowStyle=WindowStyle.None;
   var dockHandle=new WindowInteropHelper(dock).EnsureHandle();ClearDesktopDockLayer.Configure(dockHandle);dock.Show();Pump();ClearDesktopDockLayer.Refresh();
   Check("initial application surfaces remain unobscured",Unobscured(Handle(first),dockHandle)&&Unobscured(Handle(second),dockHandle));
   IntPtr foreground=GetForegroundWindow();
   SetWindowPos(dockHandle,new IntPtr(-1),0,0,0,0,0x13);Pump();ClearDesktopDockLayer.Refresh();
   Check("Dock cannot promote itself to topmost",(GetWindowLongPtr(dockHandle,-20).ToInt64()&8)==0&&Unobscured(Handle(second),dockHandle));
   Check("layer correction does not activate Dock",GetForegroundWindow()==foreground);
   Check("mouse activation is refused without consuming click",SendMessage(dockHandle,0x21,IntPtr.Zero,IntPtr.Zero)==new IntPtr(3));
   SetWindowPos(Handle(first),IntPtr.Zero,0,0,0,0,0x13);Pump();ClearDesktopDockLayer.Refresh();
   Check("application reordering does not expose Dock over apps",Unobscured(Handle(first),dockHandle)&&Unobscured(Handle(second),dockHandle));
   var late=NewWindow("Dock layer test: late app");windows.Add(late);late.Show();Pump();
   SetWindowPos(Handle(late),dockHandle,0,0,0,0,0x13);ClearDesktopDockLayer.Refresh();Pump();
   Check("new application below Dock remains unobscured",Unobscured(Handle(late),dockHandle));
   first.WindowState=WindowState.Minimized;Pump();ClearDesktopDockLayer.Refresh();first.WindowState=WindowState.Normal;Pump();
   SetWindowPos(Handle(first),dockHandle,0,0,0,0,0x13);ClearDesktopDockLayer.Refresh();
   Check("restored application remains unobscured",Unobscured(Handle(first),dockHandle));
   first.Hide();second.Hide();late.Hide();Pump();ClearDesktopDockLayer.Refresh();
   var half=NewWindow("Dock layer test: partial overlap");windows.Add(half);half.Left=dock.Left+120;half.Show();Pump();ClearDesktopDockLayer.Refresh();
   Check("partial overlap preserves uncovered Dock area",PointVisible(dockHandle,20,50)&&!PointVisible(dockHandle,180,50));
   half.Hide();Pump();ClearDesktopDockLayer.Refresh();
   bool aboveDesktop=true;int dockIndex=Index(dockHandle),index=0;
   EnumWindows((h,v)=>{var name=new StringBuilder(256);GetClassName(h,name,256);if(IsWindowVisible(h)&&(name.ToString()=="Progman"||name.ToString()=="WorkerW")&&index<dockIndex)aboveDesktop=false;index++;return true;},IntPtr.Zero);
   Check("Dock returns when application windows are hidden",IsWindowVisible(dockHandle)&&aboveDesktop&&PointVisible(dockHandle));
   return 0;
  }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
  finally{ClearDesktopDockLayer.Stop();foreach(var window in windows)window.Close();}
 }
}
