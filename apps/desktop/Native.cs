using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StudyRoomTools {
  static class Native {
    public const int GWL_EXSTYLE=-20, WS_EX_TRANSPARENT=0x20, WS_EX_TOOLWINDOW=0x80, WS_EX_NOACTIVATE=0x08000000;
    [StructLayout(LayoutKind.Sequential)] public struct RECT {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X,Y;}
    [StructLayout(LayoutKind.Sequential)] public struct MINMAXINFO {public POINT Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h,int index,int value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT rect);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr h,IntPtr region,bool redraw);
    [DllImport("user32.dll")] public static extern int GetWindowRgn(IntPtr h,IntPtr region);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
    [DllImport("gdi32.dll")] public static extern int CombineRgn(IntPtr result,IntPtr a,IntPtr b,int mode);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] public static extern bool PtInRegion(IntPtr region,int x,int y);
    public static IntPtr Handle(Window window) {return new WindowInteropHelper(window).Handle;}
    public static void PassThrough(Window window,bool enabled,bool hideFromTaskbar) {SetOverlayStyle(Handle(window),enabled,hideFromTaskbar);}
    public static void SetOverlayStyle(IntPtr h,bool passThrough,bool hideFromTaskbar) {
      if(h==IntPtr.Zero)return;
      var style=GetWindowLong(h,GWL_EXSTYLE)|WS_EX_NOACTIVATE;
      style=hideFromTaskbar?style|WS_EX_TOOLWINDOW:style&~WS_EX_TOOLWINDOW;
      style=passThrough?style|WS_EX_TRANSPARENT:style&~WS_EX_TRANSPARENT;
      SetWindowLong(h,GWL_EXSTYLE,style);
      SetWindowPos(h,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0010|0x0020);
    }
    public static void ExcludeWindows(IntPtr overlay,IEnumerable<IntPtr> controls) {
      RECT bounds;if(overlay==IntPtr.Zero||!GetWindowRect(overlay,out bounds))return;
      var holes=new List<RECT>();
      foreach(var handle in controls){RECT rectangle;if(handle!=IntPtr.Zero&&IsWindowVisible(handle)&&GetWindowRect(handle,out rectangle))holes.Add(rectangle);}
      ExcludeRects(overlay,bounds,holes);
    }
    public static void ExcludeRects(IntPtr overlay,RECT bounds,IEnumerable<RECT> controls) {
      var region=CreateRectRgn(0,0,Math.Max(0,bounds.Right-bounds.Left),Math.Max(0,bounds.Bottom-bounds.Top));
      if(region==IntPtr.Zero)return;
      try {
        foreach(var rectangle in controls) {
          var hole=CreateRectRgn(rectangle.Left-bounds.Left,rectangle.Top-bounds.Top,rectangle.Right-bounds.Left,rectangle.Bottom-bounds.Top);
          if(hole==IntPtr.Zero)continue;
          try{CombineRgn(region,region,hole,4);}finally{DeleteObject(hole);}
        }
        // Windows owns the successful region. Removing the panel/popup area also
        // prevents the drawing window from swallowing clicks if its z-order changes.
        if(SetWindowRgn(overlay,region,true)!=0)region=IntPtr.Zero;
      } finally {if(region!=IntPtr.Zero)DeleteObject(region);}
    }
  }
}
