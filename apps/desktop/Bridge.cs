using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace StudyRoomTools {
  public static class BridgeValidation {
    public static bool AllowedOrigin(string value) {return value=="https://studyroom-sut.onrender.com"||value=="http://localhost:5173"||value=="http://127.0.0.1:5173";}
    public static bool Parse(string body,out string sessionId,out bool active) {
      sessionId=null;active=false;
      try {
        var value=new JavaScriptSerializer {MaxJsonLength=512,RecursionLimit=4}.DeserializeObject(body) as Dictionary<string,object>;
        if(value==null||value.Count!=2||!value.ContainsKey("sessionId")||!value.ContainsKey("active")||!(value["sessionId"] is string)||!(value["active"] is bool))return false;
        var id=(string)value["sessionId"];Guid parsed;if(!Guid.TryParseExact(id,"D",out parsed)||parsed==Guid.Empty)return false;
        // Reject duplicate fields accepted by JavaScriptSerializer; the schema has exactly two keys.
        if(System.Text.RegularExpressions.Regex.Matches(body,"\"sessionId\"\\s*:").Count!=1||System.Text.RegularExpressions.Regex.Matches(body,"\"active\"\\s*:").Count!=1)return false;
        sessionId=parsed.ToString("D");active=(bool)value["active"];return true;
      }catch {return false;}
    }
  }
  public sealed class SharingBridge:IDisposable {
    readonly ShareLeases leases;readonly Action changed;
    readonly SemaphoreSlim clients=new SemaphoreSlim(4);
    readonly TcpListener listener;
    internal int Port {get{return ((IPEndPoint)listener.LocalEndpoint).Port;}}
    volatile bool stopping;
    public SharingBridge(ShareLeases leases,Action changed):this(leases,changed,8790){}
    internal SharingBridge(ShareLeases leases,Action changed,int port){this.leases=leases;this.changed=changed;listener=new TcpListener(IPAddress.Loopback,port);}
    public bool Start() {try{listener.Start(8);new Thread(Accept){IsBackground=true,Name="StudyRoom local share bridge"}.Start();return true;}catch(SocketException){return false;}}
    void Accept() {
      while(!stopping){try{var client=listener.AcceptTcpClient();if(!clients.Wait(0)){client.Close();continue;}Task.Run(delegate{try{Handle(client);}catch{}finally{client.Close();clients.Release();}});}catch(SocketException){if(!stopping)Thread.Sleep(100);}catch(ObjectDisposedException){return;}}
    }
    void Handle(TcpClient client) {
      client.ReceiveTimeout=2000;client.SendTimeout=2000;client.NoDelay=true;
      using(var stream=client.GetStream()) {
        stream.ReadTimeout=2000;stream.WriteTimeout=2000;
        var bytes=new List<byte>();var deadline=DateTime.UtcNow.AddSeconds(3);
        while(bytes.Count<4096) {
          if(DateTime.UtcNow>deadline)return;int next=stream.ReadByte();if(next<0)return;bytes.Add((byte)next);
          var n=bytes.Count;if(n>=4&&bytes[n-4]==13&&bytes[n-3]==10&&bytes[n-2]==13&&bytes[n-1]==10)break;
        }
        if(bytes.Count>=4096){Reply(stream,431,null);return;}
        var lines=Encoding.ASCII.GetString(bytes.ToArray()).Split(new[]{"\r\n"},StringSplitOptions.None);
        var request=lines[0].Split(' ');if(request.Length!=3||request[2]!="HTTP/1.1"){Reply(stream,400,null);return;}
        var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        for(int i=1;i<lines.Length;i++) {
          if(lines[i].Length==0)continue;var colon=lines[i].IndexOf(':');if(colon<=0){Reply(stream,400,null);return;}
          var key=lines[i].Substring(0,colon);if(key.Trim()!=key||headers.ContainsKey(key)){Reply(stream,400,null);return;}headers[key]=lines[i].Substring(colon+1).Trim();
        }
        string host,origin;if(!headers.TryGetValue("Host",out host)||host!="127.0.0.1:"+Port||!headers.TryGetValue("Origin",out origin)||!BridgeValidation.AllowedOrigin(origin)){Reply(stream,403,null);return;}
        var health=request[1]=="/v1/health";
        if(!health&&request[1]!="/v1/sharing"){Reply(stream,404,origin);return;}
        if(request[0]=="OPTIONS") {
          string method,wanted;
          var expected=health?"GET":"POST";
          bool invalidHeaders=headers.TryGetValue("Access-Control-Request-Headers",out wanted)&&wanted.Trim().Length>0&&(health||wanted.Trim().ToLowerInvariant()!="content-type");
          if(!headers.TryGetValue("Access-Control-Request-Method",out method)||method!=expected||invalidHeaders){Reply(stream,403,origin);return;}
          Reply(stream,204,origin,expected);return;
        }
        // Detection never creates a share lease, draws, reads files or activates windows.
        if(health) {
          if(request[0]!="GET"){Reply(stream,405,origin);return;}
          string bodyLength;
          if(headers.ContainsKey("Transfer-Encoding")||(headers.TryGetValue("Content-Length",out bodyLength)&&bodyLength!="0")){Reply(stream,400,origin);return;}
          Reply(stream,200,origin,null,"{\"app\":\"StudyRoom.Tools\",\"version\":\"1.1.0\",\"ready\":true}");return;
        }
        if(request[0]!="POST"){Reply(stream,405,origin);return;}
        string type,lengthValue;int length;
        if(headers.ContainsKey("Transfer-Encoding")||!headers.TryGetValue("Content-Type",out type)||type.Split(';')[0].Trim().ToLowerInvariant()!="application/json"||!headers.TryGetValue("Content-Length",out lengthValue)||!Int32.TryParse(lengthValue,out length)||length<1){Reply(stream,400,origin);return;}
        if(length>512){Reply(stream,413,origin);return;}
        var data=new byte[length];int offset=0;while(offset<length){if(DateTime.UtcNow>deadline)return;int read=stream.Read(data,offset,length-offset);if(read<=0)return;offset+=read;}
        string id;bool active;
        if(!BridgeValidation.Parse(new UTF8Encoding(false,true).GetString(data),out id,out active)){Reply(stream,400,origin);return;}
        if(!leases.Update(id,active,DateTime.UtcNow)){Reply(stream,429,origin);return;}
        changed();Reply(stream,204,origin);
      }
    }
    static void Reply(NetworkStream stream,int status,string origin,string optionsMethod=null,string body="") {
      var payload=Encoding.UTF8.GetBytes(body);
      var text="HTTP/1.1 "+status+(status==204?" No Content":status==200?" OK":" Rejected")+"\r\nContent-Length: "+payload.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\n";
      if(payload.Length>0)text+="Content-Type: application/json; charset=utf-8\r\n";
      if(origin!=null)text+="Access-Control-Allow-Origin: "+origin+"\r\nVary: Origin\r\n";
      if(optionsMethod!=null)text+="Access-Control-Allow-Methods: "+optionsMethod+"\r\n"+(optionsMethod=="POST"?"Access-Control-Allow-Headers: Content-Type\r\n":"")+"Access-Control-Allow-Private-Network: true\r\nAccess-Control-Max-Age: 300\r\n";
      var data=Encoding.ASCII.GetBytes(text+"\r\n");stream.Write(data,0,data.Length);
      if(payload.Length>0)stream.Write(payload,0,payload.Length);
    }
    public void Dispose(){stopping=true;listener.Stop();}
  }
}
