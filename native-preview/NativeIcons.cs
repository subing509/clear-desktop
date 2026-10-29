using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Windows.Interop;
using System.Windows.Threading;
public static class ClearDesktopIcons {
 [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
 public struct Info {
  public IntPtr Icon;
  public int Index;
  public uint Attributes;
  [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string DisplayName;
  [MarshalAs(UnmanagedType.ByValTStr, SizeConst=80)] public string TypeName;
 }
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
 public static extern IntPtr SHGetFileInfo(string path,uint attrs,out Info info,uint size,uint flags);
 [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern uint ExtractIconEx(string file,int index,out IntPtr large,out IntPtr small,uint count);
 public static IntPtr LoadResource(string path,int index) {
  IntPtr large,small;ExtractIconEx(Environment.ExpandEnvironmentVariables(path),index,out large,out small,1);
  if(small!=IntPtr.Zero)DestroyIcon(small);return large;
 }
 public static IntPtr Load(string path) {
  Info info;
  SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf(typeof(Info)),0x100);
  return info.Icon;
 }
}
public static class ClearDesktopDockLayer {
 delegate bool EnumProc(IntPtr window,IntPtr value);
 [StructLayout(LayoutKind.Sequential)] struct WindowPos {public IntPtr Window,After;public int X,Y,Width,Height;public uint Flags;}
 [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr value);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect bounds);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int size);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
 [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
 [DllImport("gdi32.dll")] static extern int CombineRgn(IntPtr destination,IntPtr first,IntPtr second,int mode);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr region);
 [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr window,IntPtr region,bool redraw);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
 static IntPtr Dock;
 static HwndSource Source;
 static DispatcherTimer Timer;
 static bool Reordering,RefreshQueued;
 static string MaskSignature;
 static bool ApplicationWindow(IntPtr window){
  if(window==Dock||!IsWindowVisible(window)||IsIconic(window))return false;
  long style=GetWindowLongPtr(window,-20).ToInt64();
  // Tooltips, animation proxies and the desktop are not application surfaces.
  // Never insert after a topmost window: doing so could make the Dock topmost too.
  if((style&(0x8L|0x80L|0x08000000L))!=0)return false;
  var name=new StringBuilder(256);GetClassName(window,name,name.Capacity);string cls=name.ToString();
  if(cls=="Progman"||cls=="WorkerW"||cls=="Shell_TrayWnd"||cls=="Shell_SecondaryTrayWnd"||cls=="#32768")return false;
  int cloaked;if(DwmGetWindowAttribute(window,14,out cloaked,4)==0&&cloaked!=0)return false;
  NativeRect bounds;return GetWindowRect(window,out bounds)&&bounds.Right>bounds.Left&&bounds.Bottom>bounds.Top;
 }
 static IntPtr Anchor(IntPtr application){
  // Keep entire owner groups above the Dock, including WPF's hidden owner.
  IntPtr owner=application;for(int i=0;i<16;i++){IntPtr next=GetWindow(owner,4);if(next==IntPtr.Zero||next==Dock||next==owner)break;owner=next;}
  var name=new StringBuilder(256);GetClassName(owner,name,name.Capacity);
  if(name.ToString()=="Progman"||name.ToString()=="WorkerW"||(GetWindowLongPtr(owner,-20).ToInt64()&8)!=0)return application;
  return owner;
 }
 public static void Refresh(){
  RefreshQueued=false;if(Reordering||Dock==IntPtr.Zero||!IsWindowVisible(Dock))return;
  bool seenDock=false,applicationBelow=false;IntPtr lastApplication=IntPtr.Zero;
  EnumWindows((window,value)=>{if(window==Dock){seenDock=true;return true;}if(ApplicationWindow(window)){lastApplication=Anchor(window);if(seenDock)applicationBelow=true;}return true;},IntPtr.Zero);
  bool topmost=(GetWindowLongPtr(Dock,-20).ToInt64()&8)!=0;
  if(applicationBelow||topmost){
   Reordering=true;
   try{
    if(topmost)SetWindowPos(Dock,new IntPtr(-2),0,0,0,0,0x0413);
    if(lastApplication!=IntPtr.Zero)SetWindowPos(Dock,lastApplication,0,0,0,0,0x0413);
   }finally{Reordering=false;}
  }
  ApplyOcclusionMask();
 }
 static void ApplyOcclusionMask(){
  // Some owner groups refuse lowering even when SetWindowPos reports success.
  // Clip only the covered part of our own window, including its hit-test area.
  NativeRect dockBounds;if(!GetWindowRect(Dock,out dockBounds))return;
  var covered=new List<NativeRect>();var keys=new List<string>();
  EnumWindows((window,value)=>{
   NativeRect bounds;if(!ApplicationWindow(window)||!GetWindowRect(window,out bounds))return true;
   int left=Math.Max(bounds.Left,dockBounds.Left)-dockBounds.Left,top=Math.Max(bounds.Top,dockBounds.Top)-dockBounds.Top;
   int right=Math.Min(bounds.Right,dockBounds.Right)-dockBounds.Left,bottom=Math.Min(bounds.Bottom,dockBounds.Bottom)-dockBounds.Top;
   if(right>left&&bottom>top){covered.Add(new NativeRect{Left=left,Top=top,Right=right,Bottom=bottom});keys.Add(left+","+top+","+right+","+bottom);}
   return true;
  },IntPtr.Zero);
  keys.Sort(StringComparer.Ordinal);string signature=dockBounds.Left+","+dockBounds.Top+","+dockBounds.Right+","+dockBounds.Bottom+":"+String.Join(";",keys);
  if(signature==MaskSignature)return;
  if(covered.Count==0){if(SetWindowRgn(Dock,IntPtr.Zero,true)!=0)MaskSignature=signature;return;}
  IntPtr region=CreateRectRgn(0,0,dockBounds.Right-dockBounds.Left,dockBounds.Bottom-dockBounds.Top);if(region==IntPtr.Zero)return;
  try{
   foreach(var bounds in covered){IntPtr cut=CreateRectRgn(bounds.Left,bounds.Top,bounds.Right,bounds.Bottom);if(cut==IntPtr.Zero)return;try{if(CombineRgn(region,region,cut,4)==0)return;}finally{DeleteObject(cut);}}
   if(SetWindowRgn(Dock,region,true)!=0){region=IntPtr.Zero;MaskSignature=signature;}
  }finally{if(region!=IntPtr.Zero)DeleteObject(region);}
 }
 static void QueueRefresh(){
  if(RefreshQueued||Source==null)return;RefreshQueued=true;
  Source.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(Refresh));
 }
 static IntPtr WindowMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam,ref bool handled){
  if(message==0x21){handled=true;return new IntPtr(3);} // MA_NOACTIVATE; keep the button click.
  if(message==0x46&&lParam!=IntPtr.Zero&&!Reordering){
   var position=(WindowPos)Marshal.PtrToStructure(lParam,typeof(WindowPos));
   if((position.Flags&4)==0){position.Flags|=4;Marshal.StructureToPtr(position,lParam,false);QueueRefresh();}
  }
  return IntPtr.Zero;
 }
 public static void Configure(IntPtr h) {
  Stop();Dock=h;
  long style=GetWindowLongPtr(h,-20).ToInt64();
  SetWindowLongPtr(h,-20,new IntPtr((style|0x08000080L)&~0x00040000L));
  // WPF's hidden taskbar owner constrains owned windows to stay above it.
  // A tool window needs no hidden owner and can sit below application windows.
  SetWindowLongPtr(h,-8,IntPtr.Zero);
  Source=HwndSource.FromHwnd(h);if(Source!=null){Source.AddHook(WindowMessage);Source.Disposed+=SourceDisposed;}
  Timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
  Timer.Tick+=(s,e)=>Refresh();Timer.Start();QueueRefresh();
 }
 static void SourceDisposed(object sender,EventArgs args){Stop();}
 public static void Stop(){
  if(Timer!=null){Timer.Stop();Timer=null;}
  if(Source!=null){Source.Disposed-=SourceDisposed;if(!Source.IsDisposed)Source.RemoveHook(WindowMessage);Source=null;}
  if(Dock!=IntPtr.Zero)SetWindowRgn(Dock,IntPtr.Zero,true);
  Dock=IntPtr.Zero;RefreshQueued=false;MaskSignature=null;
 }
}
