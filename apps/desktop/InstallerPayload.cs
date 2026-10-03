using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StudyRoomTools {
  public static class InstallerPayload {
    public const string Prefix="StudyRoom.Payload.";
    public static readonly string[] Names={"StudyRoom.Tools.exe","StudyRoom.Tools.exe.config","Iniciar-Herramientas.cmd","Instalar-Herramientas.cmd","Desinstalar-Herramientas.cmd","LEEME.txt","SHA256SUMS.txt"};
    public static Dictionary<string,byte[]> FromEmbedded(Assembly assembly) {
      var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
      foreach(var name in Names)using(var stream=assembly.GetManifestResourceStream(Prefix+name)) {
        if(stream==null)throw new InvalidDataException("El instalador está incompleto: "+name);
        files.Add(name,Read(stream));
      }
      Validate(files);return files;
    }
    public static Dictionary<string,byte[]> FromFolder(string folder) {
      var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
      foreach(var name in Names)using(var stream=File.OpenRead(Path.Combine(folder,name)))files.Add(name,Read(stream));
      Validate(files);return files;
    }
    static byte[] Read(Stream stream) {
      if(stream.Length>2097152)throw new InvalidDataException("Un archivo del instalador supera el límite.");
      using(var result=new MemoryStream()){stream.CopyTo(result);return result.ToArray();}
    }
    public static string Hash(byte[] data) {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
    public static void Validate(IDictionary<string,byte[]> files) {
      if(files.Count!=Names.Length)throw new InvalidDataException("El paquete contiene archivos inesperados.");
      foreach(var name in Names)if(!files.ContainsKey(name)||files[name]==null||files[name].Length==0||files[name].Length>2097152)throw new InvalidDataException("Archivo inválido: "+name);
      var lines=new UTF8Encoding(false,true).GetString(files["SHA256SUMS.txt"]).Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);
      if(lines.Length!=Names.Length-1)throw new InvalidDataException("La lista de integridad está incompleta.");
      var seen=new HashSet<string>(StringComparer.Ordinal);
      foreach(var line in lines) {
        var match=Regex.Match(line,"^([a-f0-9]{64})  ([A-Za-z0-9.\\-]+)$");
        if(!match.Success||match.Groups[2].Value=="SHA256SUMS.txt"||!files.ContainsKey(match.Groups[2].Value)||!seen.Add(match.Groups[2].Value)||Hash(files[match.Groups[2].Value])!=match.Groups[1].Value)throw new InvalidDataException("La integridad del instalador no coincide.");
      }
    }
  }
  public static class InstallationOwnership {
    public const string Owner="StudyRoom.Tools.v1",ProtocolKey="Software\\Classes\\studyroom-tools",RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run",RunName="StudyRoomTools";
    public const string PreviousExecutableHash="a698fdc358b6051af133a8dd023bf8a53d3b6c8a4d532d0bdd037560054f5982";
    public const string PreviousPublishedExecutableHash="ce95c476ada80b6d3739dc3e28d5e7d7e25273eda37e20c268713016875f228c";
    public static string StartupCommand(string target) {return "\""+Path.Combine(target,"StudyRoom.Tools.exe")+"\" --background";}
    public static string ProtocolCommand(string target) {return "\""+Path.Combine(target,"StudyRoom.Tools.exe")+"\" \"%1\"";}
    public static bool OwnsProtocol(string owner,string path,string command,string target) {return owner==Owner&&String.Equals(path,target,StringComparison.OrdinalIgnoreCase)&&String.Equals(command,ProtocolCommand(target),StringComparison.OrdinalIgnoreCase);}
    public static bool OwnsStartup(string command,string target) {return command!=null&&String.Equals(command,StartupCommand(target),StringComparison.OrdinalIgnoreCase);}
    public static bool CanRegisterStartup(object command,string target) {return command==null||(command is string&&OwnsStartup((string)command,target));}
    public static bool CanStop(string processPath,int processSession,int installerSession,string target,string hash,string currentHash,bool ownedInstallation) {
      return ownedInstallation&&processSession==installerSession&&String.Equals(processPath,Path.Combine(target,"StudyRoom.Tools.exe"),StringComparison.OrdinalIgnoreCase)&&(hash==PreviousExecutableHash||hash==PreviousPublishedExecutableHash||hash==currentHash);
    }
  }
}
