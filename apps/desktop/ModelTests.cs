using System;
using System.Windows;
using System.Windows.Media;
using StudyRoomTools;
using Geometry = StudyRoomTools.Geometry;
using System.Net.Sockets;
using System.Text;
using System.IO;
using System.Windows.Interop;
class ModelTests {
  static int assertions;
  static void Check(bool condition,string name) { assertions++;if(!condition)throw new Exception(name); }
  static Mark Line(double y) {var m=new Mark {Kind=Tool.Line,Color=Colors.Red};m.Points.Add(new Point(.1,y));m.Points.Add(new Point(.9,y));return m;}
  static string Request(int port,string method,string origin,string body,string extra="",string host=null) {
    using(var client=new TcpClient("127.0.0.1",port))using(var stream=client.GetStream()) {
      stream.ReadTimeout=5000;var text=method+" /v1/sharing HTTP/1.1\r\nHost: "+(host??("127.0.0.1:"+port))+"\r\n"+(origin==null?"":"Origin: "+origin+"\r\n")+"Content-Type: application/json\r\nContent-Length: "+Encoding.UTF8.GetByteCount(body)+"\r\n"+extra+"\r\n"+body;
      var bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);var result=new StringBuilder();var buffer=new byte[1024];try{int count;while((count=stream.Read(buffer,0,buffer.Length))>0)result.Append(Encoding.ASCII.GetString(buffer,0,count));}catch(IOException){if(result.Length==0)throw;}return result.ToString();
    }
  }
  [STAThread] static int Main() {
    try {
      var doc=new InkDocument();var first=Line(.2);Check(doc.Add(first),"add");first.Points.Clear();Check(doc.Marks[0].Points.Count==2,"immutable history copy");doc.Add(Line(.7));
      Check(doc.Erase(new Point(500,140),1000,200),"erase hit");Check(doc.Marks.Count==1,"erase one object");doc.Undo();Check(doc.Marks.Count==2,"undo erase");doc.Redo();Check(doc.Marks.Count==1,"redo erase");doc.Clear();Check(doc.Marks.Count==0,"clear");doc.Undo();Check(doc.Marks.Count==1,"undo clear");doc.Add(Line(.5));Check(!doc.CanRedo,"branch truncates redo");
      var p=Geometry.Normalize(new Point(640,360),1280,720);Check(p==new Point(.5,.5),"normalized coordinates");Check(Geometry.Pixel(p,2560,1440)==new Point(1280,720),"resolution adaptation");Check(Geometry.Normalize(new Point(-1,20),0,0)==new Point(0,1),"bounded degenerate coordinates");
      var rectangle=Line(.2);rectangle.Kind=Tool.Rectangle;rectangle.Points[1]=new Point(.8,.8);Check(Geometry.Hit(rectangle,new Point(100,500),1000,1000),"rectangle edge");Check(!Geometry.Hit(rectangle,new Point(500,500),1000,1000),"rectangle interior does not erase");
      var ellipse=rectangle.Copy();ellipse.Kind=Tool.Ellipse;Check(Geometry.Hit(ellipse,new Point(800,500),1000,1000),"ellipse edge");Check(!Geometry.Hit(ellipse,new Point(450,500),1000,1000),"ellipse interior");
      var arrow=Line(.5);arrow.Kind=Tool.Arrow;arrow.Width=12;Check(Geometry.Hit(arrow,new Point(852,524),1000,1000),"arrow wing outside shaft tolerance");Check(!Geometry.Hit(arrow,new Point(852,560),1000,1000),"arrow wing does not erase unrelated region");
      for(int i=0;i<60;i++)doc.Add(Line(i/100.0));int undo=0;while(doc.CanUndo){doc.Undo();undo++;}Check(undo<=30,"bounded history");
      doc.Reset();Check(doc.Marks.Count==0&&!doc.CanUndo&&!doc.CanRedo,"ending share removes ink and undo history");
      Check(CommandLine.Mode(new string[0])=="open","manual launch");Check(CommandLine.Mode(new[]{"studyroom-tools://open"})=="open","literal protocol");Check(CommandLine.Mode(new[]{"studyroom-tools://open?command=install"})=="invalid","no protocol parameters");Check(CommandLine.Mode(new[]{"studyroom-tools://open","--install"})=="invalid","no extra arguments");Check(CommandLine.Mode(new[]{"--install"})=="install","explicit install");
      var now=DateTime.UtcNow;var leases=new ShareLeases();var id=Guid.NewGuid().ToString("D");var id2=Guid.NewGuid().ToString("D");
      Check(leases.Update(id,true,now)&&leases.Active(now),"share active");leases.Update(id2,true,now.AddSeconds(2));leases.Update(id,false,now.AddSeconds(3));Check(leases.Active(now.AddSeconds(7)),"other tab keeps share active");Check(!leases.Active(now.AddSeconds(10)),"expired heartbeat closes share");
      leases.Update(id2,false,now.AddSeconds(10));leases.Update(id2,true,now.AddSeconds(11));Check(!leases.Active(now.AddSeconds(11)),"stale heartbeat cannot reopen stopped session");
      string parsedId;bool active;Check(BridgeValidation.Parse("{\"sessionId\":\""+id+"\",\"active\":true}",out parsedId,out active)&&active,"sharing schema");Check(!BridgeValidation.Parse("{\"sessionId\":\""+id+"\",\"active\":true,\"token\":\"x\"}",out parsedId,out active),"unknown fields");Check(!BridgeValidation.Parse("{\"sessionId\":\""+id+"\",\"active\":true,\"active\":false}",out parsedId,out active),"duplicate fields");Check(!BridgeValidation.Parse("{\"sessionId\":\"x\",\"active\":true}",out parsedId,out active),"invalid UUID");
      Check(!BridgeValidation.AllowedOrigin("https://studyroom-sut.onrender.com.evil.test"),"exact origin");
      using(var bridge=new SharingBridge(leases,delegate{},0)) {
        Check(bridge.Start(),"loopback listener");var body="{\"sessionId\":\""+id+"\",\"active\":true}";var origin="https://studyroom-sut.onrender.com";
        Check(Request(bridge.Port,"POST",origin,body).StartsWith("HTTP/1.1 204"),"authenticated-origin lifecycle request");
        Check(Request(bridge.Port,"POST",null,body).StartsWith("HTTP/1.1 403"),"missing origin rejected");Check(Request(bridge.Port,"POST","https://evil.test",body).StartsWith("HTTP/1.1 403"),"foreign origin rejected");Check(Request(bridge.Port,"POST",origin,body,"","localhost:"+bridge.Port).StartsWith("HTTP/1.1 403"),"host rebinding rejected");
        Check(Request(bridge.Port,"POST",origin,new String('x',513)).StartsWith("HTTP/1.1 413"),"body cap");Check(Request(bridge.Port,"POST",origin,"{}").StartsWith("HTTP/1.1 400"),"invalid schema HTTP");Check(Request(bridge.Port,"GET",origin,"").StartsWith("HTTP/1.1 405"),"method allowlist");
        var preflight=Request(bridge.Port,"OPTIONS",origin,"","Access-Control-Request-Method: POST\r\nAccess-Control-Request-Headers: content-type\r\n");Check(preflight.StartsWith("HTTP/1.1 204")&&preflight.Contains("Access-Control-Allow-Origin: "+origin)&&!preflight.Contains("Allow-Credentials"),"exact preflight without credentials");
      }
      // A hidden native window exercises the production style and OS input-region
      // code without showing a window, sending input or changing user focus.
      using(var source=new HwndSource(new HwndSourceParameters("StudyRoom hidden regression") {WindowStyle=0,Width=1000,Height=800})) {
        Check(!Native.IsWindowVisible(source.Handle),"native regression window stays hidden");
        Native.SetOverlayStyle(source.Handle,false,true);var style=Native.GetWindowLong(source.Handle,Native.GWL_EXSTYLE);
        Check((style&Native.WS_EX_NOACTIVATE)!=0&&(style&Native.WS_EX_TRANSPARENT)==0,"drawing receives input without activating above toolbar");
        Native.SetOverlayStyle(source.Handle,true,true);style=Native.GetWindowLong(source.Handle,Native.GWL_EXSTYLE);
        Check((style&Native.WS_EX_NOACTIVATE)!=0&&(style&Native.WS_EX_TRANSPARENT)!=0,"interact stays nonactivating and passes input");
        var bounds=new Native.RECT {Left=-1600,Top=200,Right=-600,Bottom=1000};
        var panel=new Native.RECT {Left=-900,Top=220,Right=-640,Bottom=700};
        var popup=new Native.RECT {Left=-950,Top=650,Right=-610,Bottom=850};
        var region=Native.CreateRectRgn(0,0,0,0);
        try {
          Native.ExcludeRects(source.Handle,bounds,new[]{panel,popup});Check(Native.GetWindowRgn(source.Handle,region)!=0,"native OS region applied");
          Check(!Native.PtInRegion(region,850,80),"toolbar remains outside drawing hit region");
          Check(!Native.PtInRegion(region,700,600),"expanded combo popup outside toolbar stays clickable");
          Check(Native.PtInRegion(region,100,100),"desktop outside controls remains drawable");
          var moved=new Native.RECT {Left=-1500,Top=230,Right=-1200,Bottom=500};Native.ExcludeRects(source.Handle,bounds,new[]{moved});Native.GetWindowRgn(source.Handle,region);
          Check(Native.PtInRegion(region,850,80)&&!Native.PtInRegion(region,200,100),"moving menu restores old area and excludes new area");
          Native.ExcludeRects(source.Handle,bounds,new Native.RECT[0]);Native.GetWindowRgn(source.Handle,region);Check(Native.PtInRegion(region,200,100),"hidden controls restore drawable desktop");
        } finally {Native.DeleteObject(region);}
        Check(!Native.IsWindowVisible(source.Handle),"regression did not reveal native window");
      }
      Console.WriteLine("PASS "+assertions+" assertions: drawing, history, lifecycle leases, exact protocol, loopback HTTP security, hidden native toolbar exclusion.");return 0;
    } catch(Exception e){Console.Error.WriteLine("FAIL after "+assertions+" assertions: "+e.Message);return 1;}
  }
}
