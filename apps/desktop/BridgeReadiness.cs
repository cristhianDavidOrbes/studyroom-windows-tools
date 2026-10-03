using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace StudyRoomTools {
  public static class BridgeReadiness {
    public static bool IsReady(string body) {
      if(String.IsNullOrEmpty(body)||body.Length>512)return false;
      try {
        foreach(var field in new[]{"app","version","ready"})if(Regex.Matches(body,"\""+field+"\"\\s*:").Count!=1)return false;
        var health=new JavaScriptSerializer {MaxJsonLength=512,RecursionLimit=4}.DeserializeObject(body) as Dictionary<string,object>;
        return health!=null&&health.Count==3&&health.ContainsKey("app")&&health.ContainsKey("version")&&health.ContainsKey("ready")&&
          health["app"] is string&&(string)health["app"]=="StudyRoom.Tools"&&health["version"] is string&&(string)health["version"]=="1.1.0"&&health["ready"] is bool&&(bool)health["ready"];
      }catch {return false;}
    }
    public static bool WaitForReady(Func<int,string> probe,Action<int> pause,Func<long> elapsed,int timeoutMs) {
      if(timeoutMs<1||timeoutMs>10000)throw new ArgumentOutOfRangeException("timeoutMs");
      while(elapsed()<timeoutMs) {
        var remaining=timeoutMs-elapsed();if(remaining<=0)return false;
        try {var body=probe((int)Math.Min(700,remaining));if(elapsed()>timeoutMs)return false;if(IsReady(body))return true;}catch { /* A blocked/not-yet-started app is unavailable, not a successful installation. */ }
        remaining=timeoutMs-elapsed();if(remaining<=0)return false;
        pause((int)Math.Min(250,remaining));
      }
      return false;
    }
    public static bool WaitForReady() {
      var watch=Stopwatch.StartNew();return WaitForReady(ReadHealth,Thread.Sleep,delegate{return watch.ElapsedMilliseconds;},8000);
    }
    static string ReadHealth(int timeoutMs) {
      var request=(HttpWebRequest)WebRequest.Create("http://127.0.0.1:8790/v1/health");
      request.Method="GET";request.Headers["Origin"]="https://studyroom-sut.onrender.com";
      request.Proxy=null;request.AllowAutoRedirect=false;request.KeepAlive=false;request.Timeout=timeoutMs;request.ReadWriteTimeout=timeoutMs;
      var watch=Stopwatch.StartNew();
      using(var response=(HttpWebResponse)request.GetResponse()) {
        if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>512)return null;
        using(var stream=response.GetResponseStream())using(var result=new MemoryStream()) {
          var buffer=new byte[64];int read;
          while(true) {
            var remaining=timeoutMs-watch.ElapsedMilliseconds;if(remaining<=0)throw new TimeoutException("La herramienta no respondió a tiempo.");
            if(stream.CanTimeout)stream.ReadTimeout=(int)Math.Max(1,remaining);
            read=stream.Read(buffer,0,buffer.Length);if(read==0)break;
            if(result.Length+read>512)return null;result.Write(buffer,0,read);
          }
          return new UTF8Encoding(false,true).GetString(result.ToArray());
        }
      }
    }
  }
}
