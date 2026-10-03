using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace StudyRoomTools {
  public static class CommandLine {
    public static string Mode(string[] args) {
      if(args.Length==0)return "open";
      if(args.Length!=1)return "invalid";
      if(args[0]=="studyroom-tools://open")return "open";
      if(args[0]=="--demo")return "demo";
      if(args[0]=="--install")return "install";
      if(args[0]=="--uninstall")return "uninstall";
      if(args[0]=="--background")return "background";
      return "invalid";
    }
  }
  public sealed class ShareLeases {
    readonly Dictionary<string,DateTime> leases=new Dictionary<string,DateTime>();
    readonly Dictionary<string,DateTime> stopped=new Dictionary<string,DateTime>();
    readonly object gate=new object();
    public bool Update(string sessionId,bool active,DateTime now) {
      lock(gate) {Prune(now);if(!active){leases.Remove(sessionId);stopped[sessionId]=now.AddSeconds(10);while(stopped.Count>128)stopped.Remove(stopped.OrderBy(item=>item.Value).First().Key);return true;}if(stopped.ContainsKey(sessionId))return true;if(!leases.ContainsKey(sessionId)&&leases.Count>=16)return false;leases[sessionId]=now.AddSeconds(8);return true;}
    }
    public bool Active(DateTime now) {lock(gate){Prune(now);return leases.Count>0;}}
    void Prune(DateTime now){foreach(var key in leases.Where(item=>item.Value<=now).Select(item=>item.Key).ToList())leases.Remove(key);foreach(var key in stopped.Where(item=>item.Value<=now).Select(item=>item.Key).ToList())stopped.Remove(key);}
  }
  public enum Tool { Interact, Pen, Eraser, Rectangle, Ellipse, Line, Arrow, Text }
  public sealed class Mark {
    public Tool Kind;
    public Color Color;
    public double Width = 3;
    public double FontSize = 24;
    public string Text = "";
    public readonly List<Point> Points = new List<Point>();
    public Mark Copy() { var m = new Mark { Kind=Kind, Color=Color, Width=Width, FontSize=FontSize, Text=Text }; m.Points.AddRange(Points); return m; }
  }
  public sealed class InkDocument {
    readonly List<List<Mark>> history = new List<List<Mark>> { new List<Mark>() };
    int index;
    public IList<Mark> Marks { get { return history[index].AsReadOnly(); } }
    public bool CanUndo { get { return index > 0; } }
    public bool CanRedo { get { return index + 1 < history.Count; } }
    public bool Add(Mark mark) {
      if (mark == null || mark.Points.Count == 0 || mark.Points.Count > 3000 || Marks.Count >= 300 || Marks.Sum(m=>m.Points.Count)+mark.Points.Count>100000) return false;
      var next = new List<Mark>(Marks); next.Add(mark.Copy()); Commit(next); return true;
    }
    public bool Erase(Point point, double width, double height) {
      for (int i=Marks.Count-1;i>=0;i--) if (Geometry.Hit(Marks[i],point,width,height)) { var next=new List<Mark>(Marks);next.RemoveAt(i);Commit(next);return true; }
      return false;
    }
    public void Clear() { if (Marks.Count>0) Commit(new List<Mark>()); }
    public void Reset() {history.Clear();history.Add(new List<Mark>());index=0;}
    public void Undo() { if (CanUndo) index--; }
    public void Redo() { if (CanRedo) index++; }
    void Commit(List<Mark> next) {
      if (CanRedo) history.RemoveRange(index+1,history.Count-index-1);
      history.Add(next);index++;
      while(history.Count>2 && (history.Count>31 || history.Sum(s=>s.Sum(m=>m.Points.Count))>200000)) {history.RemoveAt(0);index--;}
    }
  }
  public static class Geometry {
    public static Point Normalize(Point point,double width,double height) { return new Point(Math.Max(0,Math.Min(1,point.X/Math.Max(1,width))),Math.Max(0,Math.Min(1,point.Y/Math.Max(1,height)))); }
    public static Point Pixel(Point point,double width,double height) {return new Point(point.X*width,point.Y*height);}
    public static Rect Bounds(Point a,Point b) {return new Rect(new Point(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y)),new Point(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)));}
    public static Point[] ArrowWings(Point a,Point b,double width) {
      var vector=b-a;if(vector.Length<=.1)return new Point[]{b,b};vector.Normalize();var size=Math.Max(13,width*4);var wing=new Vector(-vector.Y,vector.X);
      return new Point[]{b-vector*size+wing*size*.5,b-vector*size-wing*size*.5};
    }
    public static double Distance(Point p,Point a,Point b) {
      var dx=b.X-a.X;var dy=b.Y-a.Y;var length=dx*dx+dy*dy;
      var t=length==0?0:Math.Max(0,Math.Min(1,((p.X-a.X)*dx+(p.Y-a.Y)*dy)/length));
      return (p-new Point(a.X+t*dx,a.Y+t*dy)).Length;
    }
    public static bool Hit(Mark mark,Point p,double width,double height) {
      if(mark.Points.Count==0)return false;
      var a=Pixel(mark.Points[0],width,height);var b=Pixel(mark.Points[mark.Points.Count-1],width,height);var tolerance=Math.Max(7,mark.Width/2+4);
      if(mark.Kind==Tool.Text) {var lines=mark.Text.Split('\n');var rect=new Rect(a,new Size(Math.Max(30,lines.Max(s=>s.Length)*mark.FontSize*.65),Math.Max(mark.FontSize*1.4,lines.Length*mark.FontSize*1.4)));rect.Inflate(5,5);return rect.Contains(p);}
      if(mark.Kind==Tool.Rectangle) {var r=Bounds(a,b);return Distance(p,r.TopLeft,r.TopRight)<=tolerance||Distance(p,r.TopRight,r.BottomRight)<=tolerance||Distance(p,r.BottomRight,r.BottomLeft)<=tolerance||Distance(p,r.BottomLeft,r.TopLeft)<=tolerance;}
      if(mark.Kind==Tool.Ellipse) {var r=Bounds(a,b);if(r.Width<1||r.Height<1)return Distance(p,a,b)<=tolerance;var cx=(a.X+b.X)/2;var cy=(a.Y+b.Y)/2;var q=Math.Sqrt(Math.Pow((p.X-cx)/(r.Width/2),2)+Math.Pow((p.Y-cy)/(r.Height/2),2));return Math.Abs(q-1)*Math.Min(r.Width,r.Height)/2<=tolerance;}
      if(mark.Kind==Tool.Arrow){var wings=ArrowWings(a,b,mark.Width);if(Distance(p,b,wings[0])<=tolerance||Distance(p,b,wings[1])<=tolerance)return true;}
      if(mark.Points.Count==1)return (p-a).Length<=tolerance;
      for(int i=1;i<mark.Points.Count;i++)if(Distance(p,Pixel(mark.Points[i-1],width,height),Pixel(mark.Points[i],width,height))<=tolerance)return true;
      return false;
    }
  }
}
