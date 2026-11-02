using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
class Installer:Form {
 TextBox folder=new TextBox(); Label status=new Label();
 static Stream Payload(){return Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");}
 static void Extract(string destination){
  string root=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
  if(Directory.Exists(root)&&Directory.GetFileSystemEntries(root).Length!=0)throw new IOException("Elige una carpeta vacía. No se reemplazarán instalaciones ni descargas existentes.");
  Directory.CreateDirectory(root);
  using(var zip=new ZipArchive(Payload(),ZipArchiveMode.Read))foreach(var e in zip.Entries){
   string p=Path.GetFullPath(Path.Combine(root,e.FullName));if(!p.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new IOException("Ruta no válida");
   Directory.CreateDirectory(Path.GetDirectoryName(p));using(var input=e.Open())using(var output=new FileStream(p,FileMode.CreateNew))input.CopyTo(output);
  }
 }
 [STAThread] static int Main(string[] args){
  try{if(args.Length==2&&args[0]=="--extract-test"){Extract(args[1]);return 0;}
   Application.EnableVisualStyles();Application.Run(new Installer());return 0;
  }catch(Exception ex){MessageBox.Show(ex.Message,"Drex Downloads · Instalador");return 1;}
 }
 Installer(){
  Text="Instalar Drex Downloads";Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);ClientSize=new Size(600,300);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(29,36,48);ForeColor=Color.WhiteSmoke;Font=new Font("Segoe UI",10);
  Controls.Add(new PictureBox{Image=Icon.ToBitmap(),SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(22,20,64,64)});
  Controls.Add(new Label{Text="DREX / DOWNLOADS",Font=new Font("Segoe UI",19,FontStyle.Bold),ForeColor=Color.FromArgb(65,215,170),Bounds=new Rectangle(100,24,480,36)});
  Controls.Add(new Label{Text="Instalación local · Sin suscripción",Bounds=new Rectangle(102,64,450,24)});
  Controls.Add(new Label{Text="Carpeta de instalación (vacía):",Bounds=new Rectangle(22,108,550,24)});
  folder.SetBounds(22,140,450,28);folder.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Drex Downloads");Controls.Add(folder);
  var browse=new Button{Text="Elegir…",Bounds=new Rectangle(482,138,95,30)};browse.Click+=(s,e)=>{using(var d=new FolderBrowserDialog())if(d.ShowDialog()==DialogResult.OK)folder.Text=d.SelectedPath;};Controls.Add(browse);
  status.SetBounds(22,183,555,48);status.Text="No modifica tus descargas. Internet exclusivo requiere instalar\nel componente de red como administrador (network/install.ps1).";Controls.Add(status);
  var install=new Button{Text="Instalar",Bounds=new Rectangle(437,245,140,36),BackColor=Color.FromArgb(65,215,170),ForeColor=Color.FromArgb(20,25,34),FlatStyle=FlatStyle.Flat};
  install.Click+=(s,e)=>{try{
   Extract(folder.Text);
   dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
   string link=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Drex Downloads.lnk");
   if(!File.Exists(link)){dynamic shortcut=shell.CreateShortcut(link);shortcut.TargetPath=Path.Combine(folder.Text,"DrexDownloads.exe");shortcut.WorkingDirectory=folder.Text;shortcut.IconLocation=shortcut.TargetPath+",0";shortcut.Save();}
   status.Text="Instalado correctamente. Abre DrexDownloads.exe en la carpeta elegida.";install.Enabled=false;folder.ReadOnly=true;browse.Enabled=false;
  }catch(Exception ex){MessageBox.Show(this,ex.Message,"No se pudo instalar",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};Controls.Add(install);
 }
}
