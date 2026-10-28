using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing=System.Drawing;

public static class ClearDesktopGenie {
 delegate bool EnumProc(IntPtr window,IntPtr value);
 delegate void WinEventProc(IntPtr hook,uint evt,IntPtr window,int obj,int child,uint thread,uint time);
 [DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint start,uint end,IntPtr module,WinEventProc callback,uint process,uint thread,uint flags);
 [DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
 [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr value);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
 [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr window);
 [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
 [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window,uint command);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window,int command);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rectangle);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr window,StringBuilder title,int size);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder name,int size);
 [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint process);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder path,ref int size);
 [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
 [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr bitmap);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window,int attr,out int value,int size);
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attr,ref int value,int size);
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr window,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr window,int index,IntPtr style);
 [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(IntPtr window,uint key,byte alpha,uint flags);
 [DllImport("user32.dll")] static extern bool GetLayeredWindowAttributes(IntPtr window,out uint key,out byte alpha,out uint flags);
 static readonly Dictionary<IntPtr,Frame> Frames=new Dictionary<IntPtr,Frame>();
 static readonly HashSet<IntPtr> Busy=new HashSet<IntPtr>();
 static readonly Dictionary<IntPtr,int> TransitionValues=new Dictionary<IntPtr,int>();
 static readonly HashSet<Window> Overlays=new HashSet<Window>();
 static readonly Dispatcher Dispatcher=Dispatcher.CurrentDispatcher;
 static int Captures;
 sealed class RecoveryRecord {public int Process,Original;public bool Cloaked;public long Deadline;public long ExStyle;public byte Alpha=255;public uint Key,Flags=2;}
 static readonly Dictionary<IntPtr,RecoveryRecord> Recovery=new Dictionary<IntPtr,RecoveryRecord>();
 static readonly HashSet<IntPtr> ManagedTransitions=new HashSet<IntPtr>();
 static readonly Dictionary<IntPtr,DateTime> IgnoreMinimize=new Dictionary<IntPtr,DateTime>(),IgnoreRestore=new Dictionary<IntPtr,DateTime>();
 static Process RecoveryGuard;static string RecoveryFile;
 static bool EnsureGuard(){
  try{if(RecoveryGuard!=null&&!RecoveryGuard.HasExited)return true;var owner=Process.GetCurrentProcess();string root=AppDomain.CurrentDomain.BaseDirectory;string exe=Path.Combine(root,"WindowRecoveryGuard.exe");if(!File.Exists(exe))return false;
   RecoveryFile=Path.Combine(root,"window-recovery-"+owner.Id+".txt");File.WriteAllText(RecoveryFile,"");
   RecoveryGuard=Process.Start(new ProcessStartInfo(exe,owner.Id+" \""+RecoveryFile+"\" "+owner.StartTime.ToUniversalTime().Ticks){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});return RecoveryGuard!=null&&!RecoveryGuard.HasExited;
  }catch{return false;}
 }
 static bool SaveRecovery(){try{var lines=new List<string>();foreach(var pair in Recovery)lines.Add(pair.Key.ToInt64()+"|"+pair.Value.Process+"|"+pair.Value.Original+"|"+(pair.Value.Cloaked?1:0)+"|"+pair.Value.Deadline+"|"+pair.Value.ExStyle+"|"+pair.Value.Alpha+"|"+pair.Value.Key+"|"+pair.Value.Flags);string temp=RecoveryFile+".tmp";File.WriteAllLines(temp,lines);if(File.Exists(RecoveryFile))File.Replace(temp,RecoveryFile,null);else File.Move(temp,RecoveryFile);return true;}catch{return false;}}
 static bool Track(IntPtr window){
  if(Recovery.ContainsKey(window))return true;if(!EnsureGuard())return false;uint pid;GetWindowThreadProcessId(window,out pid);int original;if(DwmGetWindowAttribute(window,3,out original,4)!=0)original=0;Recovery[window]=new RecoveryRecord{Process=(int)pid,Original=original};if(!SaveRecovery()){Recovery.Remove(window);return false;}return true;
 }
 static bool Arm(IntPtr window){if(!Track(window))return false;SetTransitions(window,1);ManagedTransitions.Add(window);return true;}
 static void Release(IntPtr window){RecoveryRecord record;if(!Recovery.TryGetValue(window,out record))return;if(record.Cloaked)ShowRealWindow(window);SetTransitions(window,record.Original);ManagedTransitions.Remove(window);Recovery.Remove(window);SaveRecovery();}
 static void Forget(IntPtr window){Frames.Remove(window);if(!Busy.Contains(window))Release(window);}
 static void ClearFrames(){foreach(var window in new List<IntPtr>(Frames.Keys))Forget(window);}
 static bool HideRealWindow(IntPtr window){
  if(!Track(window))return false;var record=Recovery[window];record.ExStyle=GetWindowLongPtr(window,-20).ToInt64();
  uint key=0,flags=2;byte alpha=255;
  if((record.ExStyle&0x80000)!=0&&!GetLayeredWindowAttributes(window,out key,out alpha,out flags))return false;
  if(alpha==0)return false;
  record.Key=key;record.Alpha=alpha;record.Flags=flags;record.Cloaked=true;record.Deadline=DateTime.UtcNow.AddSeconds(2).Ticks;
  if(!SaveRecovery()){record.Cloaked=false;return false;}
  SetWindowLongPtr(window,-20,new IntPtr(record.ExStyle|0x80000));
  if(SetLayeredWindowAttributes(window,0,0,2))return true;
  ShowRealWindow(window);return false;
 }
 static void ShowRealWindow(IntPtr window){
  RecoveryRecord record;if(Recovery.TryGetValue(window,out record)&&record.Cloaked){
   SetLayeredWindowAttributes(window,record.Key,record.Alpha,record.Flags);
   if((record.ExStyle&0x80000)==0){long current=GetWindowLongPtr(window,-20).ToInt64();SetWindowLongPtr(window,-20,new IntPtr(current&~0x80000L));}
   record.Cloaked=false;SaveRecovery();
  }
 }
 static bool Ignored(Dictionary<IntPtr,DateTime> events,IntPtr window){DateTime until;return events.TryGetValue(window,out until)&&DateTime.UtcNow<until;}
 static void Suppress(Dictionary<IntPtr,DateTime> events,IntPtr window){events[window]=DateTime.UtcNow.AddMilliseconds(1000);}

 static IntPtr LastExternal,ForegroundHook,MinimizeHook,LocationHook;
 static WinEventProc EventCallback;
 static FrameworkElement DefaultTarget;
 static readonly Dictionary<string,FrameworkElement> DockTargets=new Dictionary<string,FrameworkElement>(StringComparer.OrdinalIgnoreCase);
 static DispatcherTimer CaptureTimer;
 static string Family(string path){string name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();return name=="wechat"?"weixin":name;}
 static string Executable(IntPtr window){uint pid;GetWindowThreadProcessId(window,out pid);IntPtr h=OpenProcess(0x1000,false,pid);if(h==IntPtr.Zero)return "";try{int size=32768;var path=new StringBuilder(size);return QueryFullProcessImageName(h,0,path,ref size)?path.ToString():"";}finally{CloseHandle(h);}}
 static bool External(IntPtr window){uint pid;GetWindowThreadProcessId(window,out pid);return window!=IntPtr.Zero&&pid!=(uint)Process.GetCurrentProcess().Id;}
 public static void Register(string executable,FrameworkElement button){if(!String.IsNullOrEmpty(executable))DockTargets[Family(executable)]=button;}
 static Rect Target(FrameworkElement source){if(source==null||!source.IsVisible)return new Rect(SystemParameters.WorkArea.Width/2,SystemParameters.WorkArea.Bottom-50,32,32);var presentation=PresentationSource.FromVisual(source);if(presentation==null)return new Rect(SystemParameters.WorkArea.Width/2,SystemParameters.WorkArea.Bottom-50,32,32);var point=source.PointToScreen(new Point(source.ActualWidth/2,source.ActualHeight/2));point=presentation.CompositionTarget.TransformFromDevice.Transform(point);return new Rect(point.X-16,point.Y-16,32,32);}
 static Rect TargetForWindow(IntPtr window){FrameworkElement button;return Target(DockTargets.TryGetValue(Family(Executable(window)),out button)?button:DefaultTarget);}
 public static void Start(FrameworkElement fallback){
  DefaultTarget=fallback;LastExternal=GetForegroundWindow();EnsureGuard();
  CaptureTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(280)};
  CaptureTimer.Tick+=(s,e)=>{CaptureTimer.Stop();CacheForeground(LastExternal);};
  EventCallback=(hook,evt,window,obj,child,thread,time)=>{
   if(window==IntPtr.Zero)return;
   if(evt==0x800B&&(obj!=0||child!=0||window!=LastExternal))return;
   Dispatcher.BeginInvoke(new Action(()=>{
    if(!External(window))return;
    if(evt==3){LastExternal=window;CaptureTimer.Stop();CaptureTimer.Start();}
    else if(evt==0x800B){if(window==LastExternal&&!IsIconic(window)){CaptureTimer.Stop();CaptureTimer.Start();}}
    else if(evt==0x16)ObserveMinimize(window);
    else if(evt==0x17)ObserveRestore(window);
   }));
  };
  ForegroundHook=SetWinEventHook(3,3,IntPtr.Zero,EventCallback,0,0,0);
  MinimizeHook=SetWinEventHook(0x16,0x17,IntPtr.Zero,EventCallback,0,0,0);
  LocationHook=SetWinEventHook(0x800B,0x800B,IntPtr.Zero,EventCallback,0,0,0);
  LastOutcome=MinimizeHook==IntPtr.Zero?"最小化监听未启用":"已启用 Dock 与窗口最小化监听";CaptureTimer.Start();
 }
 static void CacheForeground(IntPtr window){
  if(!External(window)||!IsWindowVisible(window)||IsIconic(window)||Busy.Contains(window)||Captures>=1||Overlays.Count>0)return;
  Captures++;Task.Run(()=>Capture(window)).ContinueWith(task=>Dispatcher.BeginInvoke(new Action(()=>{
   Captures--;if(task.Status!=TaskStatus.RanToCompletion||task.Result==null||!IsWindow(window)||IsIconic(window))return;
   if(Frames.Count>=6&&!Frames.ContainsKey(window)){foreach(var key in new List<IntPtr>(Frames.Keys)){if(key!=window&&!IsIconic(key)){Forget(key);break;}}if(Frames.Count>=6)ClearFrames();}
   Frames[window]=task.Result;Arm(window);
  })));
 }
 static void ObserveMinimize(IntPtr window){
  NativeMinimizeEvents++;
  if(Busy.Contains(window)||Ignored(IgnoreMinimize,window))return;Frame frame;
  if(!SystemParameters.ClientAreaAnimation||!Frames.TryGetValue(window,out frame)||!ManagedTransitions.Contains(window)){LastOutcome="系统最小化保持原生效果：没有可接管的窗口画面";return;}
  Busy.Add(window);try{Play(frame,TargetForWindow(window),false,()=>{Busy.Remove(window);NativeAnimations++;LastOutcome="窗口按钮：吸入完成";});}catch{Busy.Remove(window);LastOutcome="窗口按钮：动画回退";}
 }
 static void ObserveRestore(IntPtr window){
  NativeRestoreEvents++;
  if(Busy.Contains(window)||Ignored(IgnoreRestore,window))return;Frame frame;if(!Frames.TryGetValue(window,out frame))return;
  if(!SystemParameters.ClientAreaAnimation)return;
  if(!HideRealWindow(window)){LastOutcome="系统恢复保持原生效果：无法安全交接窗口";return;}
  Busy.Add(window);try{Play(frame,TargetForWindow(window),true,()=>{ShowRealWindow(window);Busy.Remove(window);LastOutcome="系统恢复：展开完成";});}catch{ShowRealWindow(window);Busy.Remove(window);}
 }
 public static string LastOutcome {get;private set;}
 public static int NativeMinimizeEvents {get;private set;}
 public static int NativeRestoreEvents {get;private set;}
 public static int NativeAnimations {get;private set;}
 public static int LastRenderedFrames {get;private set;}
 public static double LastMaxFrameGapMs {get;private set;}
 public static double LastAverageUpdateMs {get;private set;}
 public static double LastAnimationDurationMs {get;private set;}
 public static double LastOverlayPixels {get;private set;}
 public static string LastAnimationMode {get;private set;}
 public static bool IsCloaked(IntPtr window){uint key,flags;byte alpha;return GetLayeredWindowAttributes(window,out key,out alpha,out flags)&&alpha==0;}
 public static string Diagnose(string executable){IntPtr window=FindWindow(executable);if(window==IntPtr.Zero)return "未匹配窗口";var task=Task.Run(()=>Capture(window));try{if(!task.Wait(650))return "取画面超时";return task.Result==null?"窗口已匹配，画面不可用":"窗口匹配及画面读取正常";}catch{return "窗口已匹配，画面读取失败";}}
 sealed class Frame {public BitmapSource Image;public Rect Bounds;public DateTime Captured=DateTime.UtcNow;}
 static void SetTransitions(IntPtr window,int value){DwmSetWindowAttribute(window,3,ref value,4);}
 static void BeginTransition(IntPtr window,int prior){Track(window);TransitionValues[window]=prior;SetTransitions(window,1);Suppress(IgnoreMinimize,window);Suppress(IgnoreRestore,window);}
 static void EndTransition(IntPtr window,int prior){if(ManagedTransitions.Contains(window))SetTransitions(window,1);else Release(window);TransitionValues.Remove(window);}
 public static void Shutdown(){if(ForegroundHook!=IntPtr.Zero)UnhookWinEvent(ForegroundHook);if(MinimizeHook!=IntPtr.Zero)UnhookWinEvent(MinimizeHook);if(LocationHook!=IntPtr.Zero)UnhookWinEvent(LocationHook);if(CaptureTimer!=null)CaptureTimer.Stop();foreach(var overlay in new List<Window>(Overlays))overlay.Close();foreach(var pair in new Dictionary<IntPtr,int>(TransitionValues))SetTransitions(pair.Key,pair.Value);TransitionValues.Clear();foreach(var window in new List<IntPtr>(Recovery.Keys))Release(window);Frames.Clear();}
 public static IntPtr FindWindow(string executable) {
  if(String.IsNullOrEmpty(executable))return IntPtr.Zero;
  IntPtr best=IntPtr.Zero;IntPtr foreground=GetForegroundWindow();
  EnumWindows((window,unused)=>{
   if(!IsWindowVisible(window))return true;
   var title=new StringBuilder(512);if(GetWindowText(window,title,512)==0)return true;
   var className=new StringBuilder(256);GetClassName(window,className,256);
   if(className.ToString()=="Progman"||className.ToString()=="WorkerW"||className.ToString()=="Shell_TrayWnd")return true;
   uint process;GetWindowThreadProcessId(window,out process);if(process==(uint)Process.GetCurrentProcess().Id)return true;
   IntPtr handle=OpenProcess(0x1000,false,process);if(handle==IntPtr.Zero)return true;
   try{var path=new StringBuilder(32768);int length=path.Capacity;if(QueryFullProcessImageName(handle,0,path,ref length)&&(String.Equals(path.ToString(),executable,StringComparison.OrdinalIgnoreCase)||Family(path.ToString())==Family(executable))){if(best==IntPtr.Zero)best=window;if(window==foreground||window==LastExternal){best=window;return false;}}}finally{CloseHandle(handle);}return true;
  },IntPtr.Zero);return best;
 }
 public static bool Handle(string executable,FrameworkElement source,bool toggle) {
  IntPtr window=FindWindow(executable);if(window==IntPtr.Zero){LastOutcome="Dock 未匹配到窗口，改为启动应用";return false;}
  if(Busy.Contains(window))return true;
  var target=Target(source);IntPtr foreground=GetForegroundWindow();if(!External(foreground))foreground=LastExternal;
  if(IsIconic(window)){Restore(window,target);return true;}
  if(toggle&&window==foreground){Minimize(window,target);return true;}
  SetForegroundWindow(window);LastExternal=window;LastOutcome="Dock 已激活窗口，再次点击可吸入";return true;
 }
 static Frame Capture(IntPtr window) {
  NativeRect bounds;if(!GetWindowRect(window,out bounds))return null;
  int width=bounds.Right-bounds.Left,height=bounds.Bottom-bounds.Top;
  if(width<10||height<10||width>10000||height>10000||((long)width*height)>24000000)return null;
  using(var bitmap=new Drawing.Bitmap(width,height,Drawing.Imaging.PixelFormat.Format32bppRgb)){
   using(var graphics=Drawing.Graphics.FromImage(bitmap)){
    IntPtr dc=graphics.GetHdc();try{if(!PrintWindow(window,dc,2))return null;}finally{graphics.ReleaseHdc(dc);}
   }
   var colors=new HashSet<int>();for(int y=height/10;y<height;y+=Math.Max(1,height/9))for(int x=width/10;x<width;x+=Math.Max(1,width/9))colors.Add(bitmap.GetPixel(x,y).ToArgb());
   if(colors.Count<3)return null;
   IntPtr handle=bitmap.GetHbitmap();try{
    BitmapSource image=Imaging.CreateBitmapSourceFromHBitmap(handle,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());
    if(width>1280||height>900){double scale=Math.Min(1280.0/width,900.0/height);image=new TransformedBitmap(image,new ScaleTransform(scale,scale));}
    image.Freeze();return new Frame{Image=image,Bounds=new Rect(bounds.Left,bounds.Top,width,height)};
   }finally{DeleteObject(handle);}
  }
 }
 public static void Minimize(IntPtr window,Rect target) {
  if(!IsWindow(window)||IsIconic(window)||Busy.Contains(window))return;
  Frame recent;
  if(SystemParameters.ClientAreaAnimation&&Frames.TryGetValue(window,out recent)&&(DateTime.UtcNow-recent.Captured).TotalMilliseconds<600){
   Busy.Add(window);int prior;DwmGetWindowAttribute(window,3,out prior,4);BeginTransition(window,prior);ShowWindowAsync(window,7);
   try{Play(recent,target,false,()=>{EndTransition(window,prior);Busy.Remove(window);LastOutcome="吸入完成";});}catch{EndTransition(window,prior);Busy.Remove(window);LastOutcome="普通最小化";}
   return;
  }
  if(!SystemParameters.ClientAreaAnimation||Captures>=2){ShowWindowAsync(window,7);LastOutcome="普通最小化";return;}
  Busy.Add(window);Captures++;bool finished=false;var timeout=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};
  timeout.Tick+=(s,e)=>{timeout.Stop();if(finished)return;finished=true;Busy.Remove(window);ShowWindowAsync(window,7);LastOutcome="截图超时，普通最小化";};timeout.Start();
  Task.Run(()=>Capture(window)).ContinueWith(task=>Dispatcher.BeginInvoke(new Action(()=>{
   Captures--;if(finished)return;finished=true;timeout.Stop();
   Frame frame=task.Status==TaskStatus.RanToCompletion?task.Result:null;
   if(frame==null||!IsWindow(window)||IsIconic(window)){Busy.Remove(window);if(IsWindow(window))ShowWindowAsync(window,7);LastOutcome="画面不可用，普通最小化";return;}
   foreach(var stale in new List<IntPtr>(Frames.Keys))if(!IsWindow(stale))Forget(stale);
   if(Frames.Count>=6)ClearFrames();Frames[window]=frame;Arm(window);
   int transitions;DwmGetWindowAttribute(window,3,out transitions,4);BeginTransition(window,transitions);
   ShowWindowAsync(window,7);
   try{Play(frame,target,false,()=>{EndTransition(window,transitions);Busy.Remove(window);LastOutcome="吸入完成";});}
   catch{EndTransition(window,transitions);Busy.Remove(window);LastOutcome="普通最小化";}
  })));
 }
 public static void Restore(IntPtr window,Rect target) {
  if(!IsWindow(window)||Busy.Contains(window))return;
  Frame frame;if(!SystemParameters.ClientAreaAnimation||!Frames.TryGetValue(window,out frame)){ShowWindowAsync(window,9);SetForegroundWindow(window);LastOutcome="普通恢复";return;}
  Busy.Add(window);int transitions;DwmGetWindowAttribute(window,3,out transitions,4);BeginTransition(window,transitions);
  Action complete=()=>{Suppress(IgnoreRestore,window);ShowWindowAsync(window,9);SetForegroundWindow(window);EndTransition(window,transitions);Busy.Remove(window);LastOutcome="展开完成";};
  try{Play(frame,target,true,complete);}catch{complete();}
 }
 static void Play(Frame frame,Rect target,bool reverse,Action completed) {
  var overlay=new Window{WindowStyle=WindowStyle.None,AllowsTransparency=true,Background=Brushes.Transparent,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=true,Left=0,Top=0,Width=16,Height=16,IsHitTestVisible=false};
  var overlayHandle=new WindowInteropHelper(overlay).EnsureHandle();var fromDevice=HwndSource.FromHwnd(overlayHandle).CompositionTarget.TransformFromDevice;
  long overlayStyle=GetWindowLongPtr(overlayHandle,-20).ToInt64();SetWindowLongPtr(overlayHandle,-20,new IntPtr(overlayStyle|0x08000000|0x20|0x80));
  Rect bounds=new Rect(fromDevice.Transform(frame.Bounds.TopLeft),fromDevice.Transform(frame.Bounds.BottomRight));
  Rect extent=Rect.Union(bounds,target);extent.Inflate(8,8);extent.Intersect(new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight));
  if(extent.IsEmpty){overlay.Close();completed();return;}
  overlay.Left=extent.Left;overlay.Top=extent.Top;overlay.Width=extent.Width;overlay.Height=extent.Height;
  bounds.Offset(-extent.Left,-extent.Top);target.Offset(-extent.Left,-extent.Top);
  LastOverlayPixels=extent.Width*extent.Height/Math.Max(.01,fromDevice.M11*fromDevice.M22);
  var visual=new GenieSurface(frame.Image,bounds,target){Width=extent.Width,Height=extent.Height};overlay.Content=visual;
  var clock=new Stopwatch();bool done=false;EventHandler render=null;var watchdog=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(650)};
  double lastTime=0,maxGap=0,totalWork=0;int updates=0,lastFrame=-1;
  Action finish=()=>{if(done)return;done=true;CompositionTarget.Rendering-=render;watchdog.Stop();Overlays.Remove(overlay);LastRenderedFrames=visual.RenderCount;LastMaxFrameGapMs=maxGap;LastAverageUpdateMs=updates>0?totalWork/updates:0;LastAnimationDurationMs=clock.Elapsed.TotalMilliseconds;try{completed();}finally{
   if(reverse){var handoff=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};handoff.Tick+=(sender,args)=>{handoff.Stop();overlay.Close();};handoff.Start();}else overlay.Close();
   if(LastAnimationMode=="轻量缩放")LastOutcome+="（轻量缩放）";if(LastRenderedFrames<2)LastOutcome="动画未产生足够画面，保留普通最小化/恢复";
  }};
  render=(sender,args)=>{
   double elapsed=clock.Elapsed.TotalMilliseconds;int frameIndex=(int)(elapsed/(1000.0/60));if(frameIndex==lastFrame)return;lastFrame=frameIndex;
   if(lastTime>0)maxGap=Math.Max(maxGap,elapsed-lastTime);lastTime=elapsed;
   double t=Math.Min(1,elapsed/340);var work=Stopwatch.StartNew();visual.Update(reverse?1-t:t);totalWork+=work.Elapsed.TotalMilliseconds;updates++;
   if(t>=1)finish();
  };
  watchdog.Tick+=(sender,args)=>finish();overlay.Closed+=(sender,args)=>{if(!done)finish();};
  visual.Update(reverse?1:0);Overlays.Add(overlay);overlay.Show();clock.Start();CompositionTarget.Rendering+=render;watchdog.Start();
 }
 sealed class GenieSurface:FrameworkElement {
  readonly List<DrawingVisual> layers=new List<DrawingVisual>();
  readonly List<MatrixTransform> transforms=new List<MatrixTransform>();
  readonly List<double> stripHeights=new List<double>();
  readonly Rect bounds,target;readonly double imageWidth;
  public int RenderCount;
  protected override int VisualChildrenCount{get{return layers.Count;}}
  protected override Visual GetVisualChild(int index){return layers[index];}
  public GenieSurface(BitmapSource image,Rect source,Rect dock){
   bounds=source;target=dock;imageWidth=image.PixelWidth;
   bool simple=(RenderCapability.Tier>>16)==0||LastOverlayPixels>2000000||LastMaxFrameGapMs>40;
   LastAnimationMode=simple?"轻量缩放":"曲线吸入";
   int count=Math.Min(simple?1:32,image.PixelHeight);
   for(int i=0;i<count;i++){
    int y=i*image.PixelHeight/count,end=(i+1)*image.PixelHeight/count;
    var strip=new CroppedBitmap(image,new Int32Rect(0,y,image.PixelWidth,end-y));strip.Freeze();
    var layer=new DrawingVisual();var transform=new MatrixTransform();layer.Transform=transform;
    RenderOptions.SetBitmapScalingMode(layer,BitmapScalingMode.LowQuality);RenderOptions.SetEdgeMode(layer,EdgeMode.Aliased);
    using(var dc=layer.RenderOpen())dc.DrawImage(strip,new Rect(0,0,image.PixelWidth,end-y));
    layers.Add(layer);transforms.Add(transform);stripHeights.Add(end-y);AddVisualChild(layer);
   }
  }
  static double Ease(double value){value=Math.Max(0,Math.Min(1,value));return value*value*(3-2*value);}
  public void Update(double progress){
   RenderCount++;
   double pinch=Ease(progress/.55),pull=Ease((progress-.20)/.80),height=bounds.Height*(1-pull)+2*pull,top=bounds.Top*(1-pull)+(target.Top+target.Height/2)*pull;
   int count=layers.Count;
   for(int i=0;i<count;i++){
    double v=(i+.5)/count,warp=pinch*v*v;
    double width=(bounds.Width*(1-warp)+target.Width*warp)*(1-pull)+target.Width*pull;
    double center=(bounds.Left+bounds.Width/2)*(1-warp)+(target.Left+target.Width/2)*warp;
    center=center*(1-pull)+(target.Left+target.Width/2)*pull;
    transforms[i].Matrix=new Matrix(Math.Max(1,width)/imageWidth,0,0,(height/count+.45)/stripHeights[i],center-width/2,top+i*height/count);
   }
  }
 }
}
