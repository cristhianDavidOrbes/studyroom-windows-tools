using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using StudyRoomTools;

class BridgeInstallerTests {
  static int assertions;
  static void Check(bool condition,string name) {assertions++;if(!condition)throw new Exception(name);}
  static void Reject(Action action,string name) {bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,name);}
  static string Request(int port,string path,string method,string origin,string body="",string extra="",string host=null) {
    using(var client=new TcpClient("127.0.0.1",port))using(var stream=client.GetStream()) {
      stream.ReadTimeout=5000;
      var text=method+" "+path+" HTTP/1.1\r\nHost: "+(host??("127.0.0.1:"+port))+"\r\n"+(origin==null?"":"Origin: "+origin+"\r\n")+"Content-Type: application/json\r\nContent-Length: "+Encoding.UTF8.GetByteCount(body)+"\r\n"+extra+"\r\n"+body;
      var bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);
      var result=new StringBuilder();var buffer=new byte[1024];int count;
      try {while((count=stream.Read(buffer,0,buffer.Length))>0)result.Append(Encoding.UTF8.GetString(buffer,0,count));}catch(IOException){if(result.Length==0)throw;}
      return result.ToString();
    }
  }
  static int Main(string[] args) {
    try {
      var leases=new ShareLeases();int changed=0;
      using(var bridge=new SharingBridge(leases,delegate{changed++;},0)) {
        Check(bridge.Start(),"isolated loopback listener starts");var origin="https://studyroom-sut.onrender.com";
        var response=Request(bridge.Port,"/v1/health","GET",origin);
        Check(response.StartsWith("HTTP/1.1 200 OK"),"health GET 200");
        Check(response.Contains("Content-Type: application/json; charset=utf-8"),"health JSON type");
        Check(response.Contains("Cache-Control: no-store"),"health never cached");
        Check(response.Contains("Access-Control-Allow-Origin: "+origin)&&!response.Contains("Allow-Credentials"),"exact origin without credential access");
        var payload=response.Substring(response.IndexOf("\r\n\r\n")+4);
        var health=new JavaScriptSerializer().DeserializeObject(payload) as Dictionary<string,object>;
        Check(health!=null&&health.Count==3&&(string)health["app"]=="StudyRoom.Tools"&&(string)health["version"]=="1.1.0"&&(bool)health["ready"],"exact bounded health contract");
        Check(changed==0&&!leases.Active(DateTime.UtcNow),"health never creates a presentation or drawing callback");
        var id=Guid.NewGuid().ToString("D");var timestamp=DateTime.UtcNow;leases.Update(id,true,timestamp.AddSeconds(-7));
        Check(Request(bridge.Port,"/v1/health","GET",origin).StartsWith("HTTP/1.1 200"),"health with active presentation");
        Check(changed==0&&leases.Active(timestamp)&&!leases.Active(timestamp.AddSeconds(2)),"health never refreshes existing share lease");
        Check(Request(bridge.Port,"/v1/health","GET",null).StartsWith("HTTP/1.1 403"),"health missing origin rejected");
        Check(Request(bridge.Port,"/v1/health","GET","https://evil.test").StartsWith("HTTP/1.1 403"),"health foreign origin rejected");
        Check(Request(bridge.Port,"/v1/health","GET",origin+".evil.test").StartsWith("HTTP/1.1 403"),"health origin prefix rejected");
        Check(Request(bridge.Port,"/v1/health","GET",origin,"","","localhost:"+bridge.Port).StartsWith("HTTP/1.1 403"),"health host rebinding rejected");
        Check(Request(bridge.Port,"/v1/health?path=C:/Users","GET",origin).StartsWith("HTTP/1.1 404"),"health does not accept paths or query parameters");
        Check(Request(bridge.Port,"/v1/health","POST",origin,"{}").StartsWith("HTTP/1.1 405"),"health mutation method rejected");
        Check(Request(bridge.Port,"/v1/health","GET",origin,"{}").StartsWith("HTTP/1.1 400"),"health body rejected");
        var options=Request(bridge.Port,"/v1/health","OPTIONS",origin,"","Access-Control-Request-Method: GET\r\nAccess-Control-Request-Private-Network: true\r\n");
        Check(options.StartsWith("HTTP/1.1 204")&&options.Contains("Access-Control-Allow-Methods: GET")&&options.Contains("Access-Control-Allow-Private-Network: true"),"health GET local-network preflight");
        Check(!options.Contains("Access-Control-Allow-Headers:"),"health does not allow arbitrary custom headers");
        Check(Request(bridge.Port,"/v1/health","OPTIONS",origin,"","Access-Control-Request-Method: POST\r\n").StartsWith("HTTP/1.1 403"),"health preflight POST rejected");
        Check(Request(bridge.Port,"/v1/health","OPTIONS",origin,"","Access-Control-Request-Method: GET\r\nAccess-Control-Request-Headers: authorization\r\n").StartsWith("HTTP/1.1 403"),"health authorization header rejected");
        Check(changed==0&&!leases.Active(DateTime.UtcNow.AddSeconds(2)),"all health probes leave lifecycle unchanged");
        var body="{\"sessionId\":\""+Guid.NewGuid().ToString("D")+"\",\"active\":true}";
        Check(Request(bridge.Port,"/v1/sharing","POST",origin,body).StartsWith("HTTP/1.1 204")&&changed==1&&leases.Active(DateTime.UtcNow),"existing share POST still activates only its callback");
        Check(Request(bridge.Port,"/v1/sharing","GET",origin).StartsWith("HTTP/1.1 405"),"sharing GET remains rejected");
        Check(Request(bridge.Port,"/v1/sharing","POST",origin,new String('x',513)).StartsWith("HTTP/1.1 413"),"sharing size limit preserved");
        Check(Request(bridge.Port,"/v1/sharing","OPTIONS",origin,"","Access-Control-Request-Method: POST\r\nAccess-Control-Request-Headers: content-type\r\n").Contains("Access-Control-Allow-Methods: POST"),"sharing JSON POST preflight preserved");
      }
      var assembly=Assembly.ReflectionOnlyLoad(File.ReadAllBytes(args[0]));
      Check(assembly.GetName().Version==new Version(1,1,0,0),"installer assembly version");
      Check(assembly.GetManifestResourceNames().Length==InstallerPayload.Names.Length,"installer embeds only seven allowed files");
      var files=InstallerPayload.FromEmbedded(assembly);
      foreach(var name in InstallerPayload.Names)Check(InstallerPayload.Hash(files[name])==InstallerPayload.Hash(File.ReadAllBytes(Path.Combine(args[1],name))),"embedded actual file integrity: "+name);
      Check(files["StudyRoom.Tools.exe"][0]=='M'&&files["StudyRoom.Tools.exe"][1]=='Z',"embedded actual Windows executable");
      var changedFiles=new Dictionary<string,byte[]>(files);changedFiles["LEEME.txt"]=Encoding.UTF8.GetBytes("tampered");Reject(delegate{InstallerPayload.Validate(changedFiles);},"changed embedded content rejected");
      changedFiles=new Dictionary<string,byte[]>(files);changedFiles.Add("../unexpected.exe",new byte[]{1});Reject(delegate{InstallerPayload.Validate(changedFiles);},"unexpected extraction path rejected");
      changedFiles=new Dictionary<string,byte[]>(files);changedFiles["SHA256SUMS.txt"]=Encoding.UTF8.GetBytes(InstallerPayload.Hash(files["LEEME.txt"])+"  ../LEEME.txt\n");Reject(delegate{InstallerPayload.Validate(changedFiles);},"malformed integrity manifest rejected");
      var target=Path.GetFullPath(Path.Combine("C:\\Users\\Synthetic User\\AppData\\Local","StudyRoom","Herramientas"));
      Check(InstallationOwnership.CanRegisterStartup(null,target),"new startup can be registered");
      Check(InstallationOwnership.CanRegisterStartup(InstallationOwnership.StartupCommand(target),target),"own exact startup can be reused");
      Check(!InstallationOwnership.CanRegisterStartup("other.exe",target)&&!InstallationOwnership.CanRegisterStartup(3,target),"foreign startup values never overwritten");
      Check(!InstallationOwnership.OwnsStartup(InstallationOwnership.StartupCommand(target)+" --extra",target),"uninstall leaves modified startup value");
      Check(InstallationOwnership.OwnsProtocol(InstallationOwnership.Owner,target,InstallationOwnership.ProtocolCommand(target),target),"own exact protocol");
      Check(!InstallationOwnership.OwnsProtocol("other",target,InstallationOwnership.ProtocolCommand(target),target)&&!InstallationOwnership.OwnsProtocol(InstallationOwnership.Owner,target,"other.exe",target),"foreign ownership and command rejected");
      var executable=Path.Combine(target,"StudyRoom.Tools.exe");var currentHash=InstallerPayload.Hash(files["StudyRoom.Tools.exe"]);
      Check(InstallationOwnership.CanStop(executable,1,1,target,InstallationOwnership.PreviousExecutableHash,currentHash,true),"only known earlier owned executable can upgrade");
      Check(InstallationOwnership.CanStop(executable,1,1,target,InstallationOwnership.PreviousPublishedExecutableHash,currentHash,true),"previous published 1.1 helper can upgrade only by exact hash");
      Check(InstallationOwnership.CanStop(executable,1,1,target,currentHash,currentHash,true),"own current verified executable can be restarted");
      Check(!InstallationOwnership.CanStop(executable,2,1,target,currentHash,currentHash,true),"another Windows session cannot be stopped");
      Check(!InstallationOwnership.CanStop(executable,1,1,target,currentHash,currentHash,false),"unowned installation cannot be stopped");
      Check(!InstallationOwnership.CanStop(executable,1,1,target,"unknown",currentHash,true),"unknown executable cannot be stopped");
      Check(!InstallationOwnership.CanStop("C:\\Other\\StudyRoom.Tools.exe",1,1,target,currentHash,currentHash,true),"same process name in other path cannot be stopped");
      var ready="{\"app\":\"StudyRoom.Tools\",\"version\":\"1.1.0\",\"ready\":true}";
      Check(BridgeReadiness.IsReady(ready),"installer readiness accepts current exact health");
      Check(!BridgeReadiness.IsReady(null)&&!BridgeReadiness.IsReady("{}")&&!BridgeReadiness.IsReady("not-json"),"missing and invalid health cannot confirm installation");
      Check(!BridgeReadiness.IsReady(ready.Replace("true","false")),"not-ready helper rejected");
      Check(!BridgeReadiness.IsReady(ready.Replace("1.1.0","1.0.0")),"old helper version rejected");
      Check(!BridgeReadiness.IsReady(ready.Replace("StudyRoom.Tools","Other.App")),"different local app rejected");
      Check(!BridgeReadiness.IsReady(ready.Replace("true","\"true\"")),"health readiness requires a Boolean");
      Check(!BridgeReadiness.IsReady(ready.Replace("}",",\"ready\":true}")),"duplicate readiness field rejected");
      Check(!BridgeReadiness.IsReady(ready.Replace("}",",\"extra\":1}")),"extra health fields rejected");
      long clock=0;int attempts=0,pauses=0;
      Check(BridgeReadiness.WaitForReady(delegate(int budget){attempts++;Check(budget<=700&&budget>0,"probe has a bounded timeout");return ready;},delegate(int duration){pauses++;clock+=duration;},delegate{return clock;},1000)&&attempts==1&&pauses==0,"ready child confirms immediately");
      clock=0;attempts=0;pauses=0;
      Check(!BridgeReadiness.WaitForReady(delegate(int budget){attempts++;return null;},delegate(int duration){pauses++;clock+=duration;},delegate{return clock;},1000)&&clock==1000&&attempts==4&&pauses==4,"unavailable child stops at bounded deadline");
      clock=0;attempts=0;
      Check(BridgeReadiness.WaitForReady(delegate(int budget){return ++attempts==2?ready:null;},delegate(int duration){clock+=duration;},delegate{return clock;},1000)&&attempts==2,"child can become ready during startup");
      clock=0;
      Check(!BridgeReadiness.WaitForReady(delegate(int budget){clock+=budget;throw new IOException("child blocked");},delegate(int duration){clock+=duration;},delegate{return clock;},1000)&&clock==1000,"blocked child does not produce false success or unbounded retries");
      clock=0;
      Check(!BridgeReadiness.WaitForReady(delegate(int budget){clock=1001;return ready;},delegate(int duration){clock+=duration;},delegate{return clock;},1000),"late response after deadline cannot confirm installation");
      Console.WriteLine("PASS "+assertions+" assertions: read-only health HTTP, unchanged share leases, standalone embedded integrity and pure startup/upgrade ownership. No windows, installer execution, process stop or registry write.");return 0;
    }catch(Exception error){Console.Error.WriteLine("FAIL after "+assertions+" assertions: "+error.Message);return 1;}
  }
}
