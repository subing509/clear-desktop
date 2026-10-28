using System;
using System.Runtime.InteropServices;
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
 [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
 [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
 public static void Configure(IntPtr h) {
  long style=GetWindowLongPtr(h,-20).ToInt64();
  SetWindowLongPtr(h,-20,new IntPtr(style|0x08000000L));
  IntPtr foreground=GetForegroundWindow();
  if(foreground!=IntPtr.Zero && foreground!=h) SetWindowPos(h,foreground,0,0,0,0,0x0013);
 }
}
