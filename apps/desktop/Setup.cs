using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace StudyRoomTools {
  public static class Setup {
    const string Marker="StudyRoom.installation";
    public static int Run(bool install) {return Run(install,delegate{return InstallerPayload.FromFolder(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));});}
    public static int InstallEmbedded(Assembly assembly) {return Run(true,delegate{return InstallerPayload.FromEmbedded(assembly);});}
    static int Run(bool install,Func<Dictionary<string,byte[]>> getFiles) {
      try {
        var parent=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StudyRoom"));
        var target=Path.GetFullPath(Path.Combine(parent,"Herramientas"));
        if(!target.StartsWith(parent+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Ruta no válida.");
        if((Directory.Exists(parent)&&(File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)||(Directory.Exists(target)&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0))throw new InvalidOperationException("La carpeta de instalación no puede ser un enlace a otra ubicación.");
        bool ownsProtocol=false;
        using(var existing=Registry.CurrentUser.OpenSubKey(InstallationOwnership.ProtocolKey)) {
          if(existing!=null)using(var command=existing.OpenSubKey("shell\\open\\command")) {
            ownsProtocol=InstallationOwnership.OwnsProtocol(existing.GetValue("StudyRoomOwner") as string,existing.GetValue("StudyRoomPath") as string,command==null?null:command.GetValue("") as string,target);
            if(!ownsProtocol)throw new InvalidOperationException("El protocolo pertenece a otra instalación. No se modificó.");
          }
        }
        var marker=Path.Combine(target,Marker);var markerValue=InstallationOwnership.Owner+"\n"+target;
        if(File.Exists(marker)&&(File.GetAttributes(marker)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("La marca de instalación no puede ser un enlace.");
        bool ownsMarker=File.Exists(marker)&&File.ReadAllText(marker)==markerValue;
        var executable=Path.Combine(target,"StudyRoom.Tools.exe");
        if(install) {
          using(var run=Registry.CurrentUser.OpenSubKey(InstallationOwnership.RunKey))if(run!=null&&!InstallationOwnership.CanRegisterStartup(run.GetValue(InstallationOwnership.RunName),target))throw new InvalidOperationException("La entrada de inicio pertenece a otra aplicación. No se modificó.");
          var files=getFiles(); // Verify every embedded file before changing registry or installation.
          foreach(var name in InstallerPayload.Names) {
            var candidate=Path.Combine(target,name);
            if(File.Exists(candidate)&&(File.GetAttributes(candidate)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("Un archivo de instalación es un enlace. No se modificó.");
          }
          if(File.Exists(executable)&&InstallerPayload.Hash(File.ReadAllBytes(executable))!=InstallerPayload.Hash(files["StudyRoom.Tools.exe"]))StopOwnTools(target,InstallerPayload.Hash(files["StudyRoom.Tools.exe"]),ownsProtocol||ownsMarker);
          Directory.CreateDirectory(target);
          foreach(var name in InstallerPayload.Names) {
            var to=Path.Combine(target,name);
            if(!File.Exists(to)||InstallerPayload.Hash(File.ReadAllBytes(to))!=InstallerPayload.Hash(files[name]))File.WriteAllBytes(to,files[name]);
          }
          File.WriteAllText(marker,markerValue);
          using(var key=Registry.CurrentUser.CreateSubKey(InstallationOwnership.ProtocolKey)) {
            key.SetValue("","URL:Herramientas StudyRoom");key.SetValue("URL Protocol","");key.SetValue("StudyRoomOwner",InstallationOwnership.Owner);key.SetValue("StudyRoomPath",target);
            using(var command=key.CreateSubKey("shell\\open\\command"))command.SetValue("",InstallationOwnership.ProtocolCommand(target));
          }
          using(var run=Registry.CurrentUser.CreateSubKey(InstallationOwnership.RunKey)) {
            if(!InstallationOwnership.CanRegisterStartup(run.GetValue(InstallationOwnership.RunName),target))throw new InvalidOperationException("Otra aplicación registró una entrada de inicio. No se sobrescribió.");
            run.SetValue(InstallationOwnership.RunName,InstallationOwnership.StartupCommand(target));
          }
          Shortcut(executable,false);
          try {using(var started=Process.Start(new ProcessStartInfo(executable,"--background"){UseShellExecute=false,CreateNoWindow=true})) {}}
          catch(Exception error) {
            MessageBox.Show("Los archivos se instalaron para tu usuario, pero Windows no pudo iniciar las herramientas.\n\n"+error.Message+"\n\nRevisa el aviso de Windows. Si indica que no puede verificar el publicador, necesitas una distribución firmada de confianza. El programa no modifica tus protecciones.","StudyRoom: herramienta no disponible",MessageBoxButton.OK,MessageBoxImage.Warning);return 1;
          }
          if(!BridgeReadiness.WaitForReady()) {
            MessageBox.Show("Los archivos se instalaron para tu usuario, pero las herramientas todavía no están disponibles. La página no podrá confirmarlas hasta que respondan.\n\nComprueba si Windows mostró un bloqueo al abrir la aplicación o si hay otra aplicación usando el puerto local 8790. Si Windows no puede verificar el publicador, esta descarga necesita una distribución firmada de confianza.\n\nNo se cambiaron tus protecciones. Puedes volver a abrir el instalador cuando la aplicación pueda iniciarse.","StudyRoom: instalación pendiente de apertura",MessageBoxButton.OK,MessageBoxImage.Warning);return 1;
          }
          MessageBox.Show("Listo. Vuelve a StudyRoom, espera el mensaje «La aplicación ya está instalada» y pulsa Continuar.\n\nLas herramientas aparecerán al compartir pantalla y se ocultarán al terminar. Se iniciarán en segundo plano al entrar a Windows; no necesitas extraer un ZIP ni abrirlas cada vez.\nEl navegador puede solicitar permiso de acceso a aplicaciones locales.","StudyRoom instalado",MessageBoxButton.OK,MessageBoxImage.Information);
        } else {
          if(!ownsProtocol&&!ownsMarker)throw new InvalidOperationException("No se encontró una instalación propia. No se borró ningún archivo.");
          // Preserve a startup entry changed by another application, even during uninstall.
          using(var run=Registry.CurrentUser.OpenSubKey(InstallationOwnership.RunKey,true))if(run!=null&&InstallationOwnership.OwnsStartup(run.GetValue(InstallationOwnership.RunName) as string,target))run.DeleteValue(InstallationOwnership.RunName,false);
          if(ownsProtocol)Registry.CurrentUser.DeleteSubKeyTree(InstallationOwnership.ProtocolKey,false);
          Shortcut(executable,true);
          bool remaining=false;
          foreach(var name in InstallerPayload.Names) {
            var file=Path.Combine(target,name);
            if(File.Exists(file)) {if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0){remaining=true;continue;}try{File.Delete(file);}catch(IOException){remaining=true;}catch(UnauthorizedAccessException){remaining=true;}}
          }
          if(!remaining&&ownsMarker)File.Delete(marker);
          MessageBox.Show(remaining?"El inicio automático, protocolo y acceso directo propios fueron retirados.\nCierra las herramientas y ejecuta de nuevo Desinstalar-Herramientas.cmd desde el ZIP de respaldo para retirar los archivos en uso.":"El inicio automático, protocolo, acceso directo y archivos propios fueron retirados. No se modificaron otras aplicaciones.","StudyRoom",MessageBoxButton.OK,MessageBoxImage.Information);
        }
        return 0;
      }catch(Exception error){MessageBox.Show("No se completó la operación.\n\n"+error.Message,"Herramientas StudyRoom",MessageBoxButton.OK,MessageBoxImage.Warning);return 1;}
    }
    static void StopOwnTools(string target,string currentHash,bool owned) {
      var executable=Path.Combine(target,"StudyRoom.Tools.exe");var installedHash=InstallerPayload.Hash(File.ReadAllBytes(executable));
      var installer=Process.GetCurrentProcess();
      foreach(var process in Process.GetProcessesByName("StudyRoom.Tools"))using(process) {
        if(process.Id==installer.Id)continue;
        string path;try{path=process.MainModule.FileName;}catch {continue;}
        if(!InstallationOwnership.CanStop(path,process.SessionId,installer.SessionId,target,installedHash,currentHash,owned))continue;
        // New versions exit through their own event; the known older release has no exit event.
        EventWaitHandle quit;
        if(EventWaitHandle.TryOpenExisting("Local\\StudyRoom.Tools.Exit."+WindowsIdentity.GetCurrent().User.Value,out quit))using(quit){quit.Set();process.WaitForExit(2000);}
        if(!process.HasExited) {
          // Recheck the exact target and image hash immediately before stopping this owned app.
          if(!InstallationOwnership.CanStop(process.MainModule.FileName,process.SessionId,installer.SessionId,target,InstallerPayload.Hash(File.ReadAllBytes(executable)),currentHash,owned))throw new InvalidOperationException("La aplicación cambió durante la actualización. Cierra las herramientas y vuelve a abrir el instalador.");
          process.Kill();if(!process.WaitForExit(5000))throw new InvalidOperationException("No se pudo cerrar la versión anterior de StudyRoom.");
        }
      }
    }
    static void Shortcut(string executable,bool remove) {
      var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Herramientas StudyRoom.lnk");
      try {
        var type=Type.GetTypeFromProgID("WScript.Shell");if(type==null)return;
        var shell=Activator.CreateInstance(type);var shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
        var current=(string)shortcut.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null);
        if(File.Exists(path)&&!String.Equals(current,executable,StringComparison.OrdinalIgnoreCase))return;
        if(remove){if(File.Exists(path))File.Delete(path);return;}
        shortcut.GetType().InvokeMember("TargetPath",BindingFlags.SetProperty,null,shortcut,new object[]{executable});
        shortcut.GetType().InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,shortcut,new object[]{Path.GetDirectoryName(executable)});
        shortcut.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,shortcut,null);
      }catch { /* Protocol and startup remain usable if shortcuts are restricted. */ }
    }
  }
}
