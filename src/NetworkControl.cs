using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Security.Principal;
namespace DrexDownloads {
static class NetworkControl {
 static string Root {get{return AppDomain.CurrentDomain.BaseDirectory;}}
 public static bool Ready(){try{return File.ReadAllText(Path.Combine(Root,"network","integrated.version"))=="1.0.6" && DateTime.UtcNow-File.GetLastWriteTimeUtc(Path.Combine(Root,"network","status.txt"))<TimeSpan.FromSeconds(8);}catch{return false;}}
 public static void Ensure(){
  if(Ready())return;
  var p=Process.Start(new ProcessStartInfo(System.Windows.Forms.Application.ExecutablePath,"--network-guard"){UseShellExecute=true,Verb="runas",WindowStyle=ProcessWindowStyle.Hidden});
  for(int i=0;i<80;i++){if(Ready())return;if(p.HasExited)throw new IOException("No se pudo iniciar el control de red integrado. Consulta network/helper-error.txt.");Thread.Sleep(100);}
  throw new IOException("El control de red no confirmó el inicio. Las descargas normales siguen disponibles.");
 }
 public static int Run(string recoveryRoot=null){
  string runRoot=recoveryRoot??Root;
  try{
   if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException("Se requieren permisos de administrador para el firewall.");
   string script;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Drex.NetworkGuard"))using(var reader=new StreamReader(stream))script=reader.ReadToEnd();
   string encodedRoot=Convert.ToBase64String(Encoding.UTF8.GetBytes(runRoot.TrimEnd(Path.DirectorySeparatorChar)));
   script="$DrexRecoveryOnly="+(recoveryRoot==null?"$false":"$true")+"\n$DrexRoot=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+encodedRoot+"'))\n"+script;
   var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"),"-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
   using(var child=Process.Start(start)){string error=child.StandardError.ReadToEnd();child.WaitForExit();if(child.ExitCode!=0)throw new IOException(error);return 0;}
  }catch(Exception ex){Directory.CreateDirectory(Path.Combine(runRoot,"network"));File.WriteAllText(Path.Combine(runRoot,"network","helper-error.txt"),ex.ToString());return 1;}
 }
}
}
