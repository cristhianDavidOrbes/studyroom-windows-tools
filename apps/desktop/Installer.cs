using System;
using System.Reflection;
using System.Windows;

namespace StudyRoomTools {
  public static class Installer {
    [STAThread] public static int Main(string[] args) {
      if(args.Length!=0) {MessageBox.Show("Abre el instalador sin argumentos para preparar las herramientas de StudyRoom.","StudyRoom",MessageBoxButton.OK,MessageBoxImage.Information);return 2;}
      return Setup.InstallEmbedded(Assembly.GetExecutingAssembly());
    }
  }
}
