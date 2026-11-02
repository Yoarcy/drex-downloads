using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DrexDownloads {
static class Program {
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
 [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr handle,int command);
 [STAThread] static int Main(string[] args){
  if(args.Length>0&&args[0]=="--network-guard")return NetworkControl.Run();
  if(args.Length==2&&args[0]=="--network-recover")return NetworkControl.Run(Encoding.UTF8.GetString(Convert.FromBase64String(args[1])));
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length>0&&args[0]=="--test-download")return TestDownload(args);
  if(args.Length>0&&args[0]=="--verify-state")return Verify(args);
  bool first;using(var mutex=new Mutex(true,"Local\\DrexDownloads-Yoa-1",out first)){
   if(!first){foreach(var p in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))if(p.Id!=Process.GetCurrentProcess().Id){ShowWindow(p.MainWindowHandle,9);SetForegroundWindow(p.MainWindowHandle);}File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"show.request"),"show");return 0;}
   try{Application.Run(new MainForm());return 0;}catch(Exception ex){MessageBox.Show("No se pudo abrir Drex Downloads. Tus descargas no se borraron.\n"+ex.Message,"Drex Downloads",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
  }
 }
 static int Verify(string[] args){try{using(var e=new Engine(args[1])){foreach(var j in e.Jobs){e.Recover(j);File.WriteAllText(Path.Combine(args[1],"verified.json"),Encoding.UTF8.GetString(Store.Serialize(j.D)));}return 0;}}catch{return 2;}}
 static int TestDownload(string[] args){
  string root=args[1];Directory.CreateDirectory(root);using(var e=new Engine(root)){Job j=e.Jobs.FirstOrDefault();try{
   if(j==null)j=e.Add(args[2],args[3],args[4],args.Length>5?args[5]:"",args.Length>6?int.Parse(args[6]):2,args.Length>7?int.Parse(args[7]):0,"",true);
   else {j.Stop=false;j.D.Desired=true;if(args.Length>7)j.D.LimitKiB=int.Parse(args[7]);if(args.Length>2&&args[2]!="-"){j.D.Url=args[2];e.Save(j);}}
   e.Run(j);File.WriteAllText(Path.Combine(root,"result.json"),Encoding.UTF8.GetString(Store.Serialize(j.D)));return 0;
  }catch(Exception ex){if(j!=null){j.D.Error=ex.Message;e.Save(j);File.WriteAllText(Path.Combine(root,"result.json"),Encoding.UTF8.GetString(Store.Serialize(j.D)));}return 2;}}
 }
}
public class TexturePanel:Panel {
 public TexturePanel(){DoubleBuffered=true;}
 protected override void OnPaintBackground(PaintEventArgs e){
  using(var brush=new LinearGradientBrush(ClientRectangle,ControlPaint.Light(BackColor,.035f),BackColor,90f))e.Graphics.FillRectangle(brush,ClientRectangle);
  using(var pen=new Pen(Color.FromArgb(8,255,255,255)))for(int y=0;y<Height;y+=4)e.Graphics.DrawLine(pen,0,y,Width,y);
 }
}
public class SoftButton:Button {
 bool hot,down;
 public SoftButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
 static GraphicsPath Round(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 protected override void OnMouseEnter(EventArgs e){hot=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hot=false;down=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.Clear(Parent==null?Color.FromArgb(20,25,34):Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;
  var rect=new Rectangle(1,down?3:1,Width-4,Height-6);
  using(var shadow=Round(new Rectangle(2,4,Width-4,Height-6),7))using(var b=new SolidBrush(Color.FromArgb(100,0,0,0)))g.FillPath(b,shadow);
  Color c=hot?ControlPaint.Light(BackColor,.15f):BackColor;
  using(var path=Round(rect,7)){
   using(var b=new LinearGradientBrush(rect,ControlPaint.Light(c,.13f),c,90f))g.FillPath(b,path);
   using(var pen=new Pen(Color.FromArgb(40,255,255,255)))g.DrawPath(pen,path);
  }
  TextRenderer.DrawText(g,Text,Font,rect,Enabled?ForeColor:Color.Gray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-13,Height-15),ForeColor,c);
 }
}
public class MainForm:Form {
 CheckBox exclusive=new CheckBox();Label networkState=new Label();
 Engine engine;DataGridView grid=new DataGridView();Label stats=new Label(),status=new Label();TextBox filter=new TextBox();NotifyIcon tray;System.Windows.Forms.Timer timer;bool exiting=false;Dictionary<string,long> previous=new Dictionary<string,long>();DateTime tick=DateTime.UtcNow;string root=AppDomain.CurrentDomain.BaseDirectory;
 Color bg=Color.FromArgb(20,25,34),panel=Color.FromArgb(29,36,48),accent=Color.FromArgb(65,215,170),fg=Color.FromArgb(230,236,245);
 public MainForm(){
  Text="Drex Downloads · Descargas protegidas";Name="DrexDownloads";ClientSize=new Size(760,454);FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;BackColor=bg;ForeColor=fg;Font=new Font("Segoe UI",9);Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  engine=new Engine(Path.Combine(root,"data"));
  var header=new TexturePanel{Dock=DockStyle.Top,Height=66,BackColor=panel,Padding=new Padding(14,8,14,6)};
  var title=new Label{Text="DREX  /  DOWNLOADS",Font=new Font("Segoe UI",17,FontStyle.Bold),ForeColor=accent,BackColor=Color.Transparent,AutoSize=true,Location=new Point(14,7)};
  var subtitle=new Label{Text="Tus archivos. Tu progreso. Sin suscripción.",AutoSize=true,Location=new Point(16,40),BackColor=Color.Transparent,ForeColor=Color.Silver};header.Controls.AddRange(new Control[]{title,subtitle});Controls.Add(header);
  var toolbar=new FlowLayoutPanel{Dock=DockStyle.Top,Height=44,Padding=new Padding(10,5,6,3),WrapContents=false,BackColor=bg};
  toolbar.Controls.Add(Button("+ Nueva descarga",()=>AddDownload(),true));toolbar.Controls.Add(Button("Pausar",()=>Act(j=>engine.Pause(j))));toolbar.Controls.Add(Button("Continuar",()=>Act(j=>engine.Resume(j))));toolbar.Controls.Add(Button("Renovar enlace",Renew));toolbar.Controls.Add(Button("Abrir carpeta",OpenFolder));toolbar.Controls.Add(Button("Quitar",RemoveSelected));toolbar.Controls.Add(Button("Más ▾",More));Controls.Add(toolbar);toolbar.BringToFront();
  var searchBar=new Panel{Dock=DockStyle.Top,Height=32,Padding=new Padding(14,2,14,4)};
  filter.Width=205;filter.Location=new Point(14,3);filter.BackColor=panel;filter.ForeColor=fg;filter.BorderStyle=BorderStyle.FixedSingle;filter.TextChanged+=(s,e)=>RefreshGrid();
  var searchLabel=new Label{Text="Buscar por nombre",AutoSize=true,Location=new Point(230,5),ForeColor=Color.Silver};searchBar.Controls.AddRange(new Control[]{filter,searchLabel});exclusive.Text="Internet exclusivo";exclusive.AutoSize=true;exclusive.Location=new Point(370,4);exclusive.Checked=File.Exists(Path.Combine(root,"network","enabled.flag"));
  exclusive.CheckedChanged+=(s,e)=>{try{if(exclusive.Checked){NetworkLease();NetworkControl.Ensure();}string f=Path.Combine(root,"network","enabled.flag");if(exclusive.Checked)File.WriteAllText(f,"enabled");else if(File.Exists(f))File.Delete(f);NetworkLease();}catch(Exception ex){exclusive.Checked=false;MessageBox.Show(ex.Message);}};
  networkState.Location=new Point(510,5);networkState.Size=new Size(240,22);networkState.AutoEllipsis=true;networkState.ForeColor=Color.Silver;searchBar.Controls.Add(exclusive);searchBar.Controls.Add(networkState);
  Controls.Add(searchBar);searchBar.BringToFront();
  var footer=new TexturePanel{Dock=DockStyle.Bottom,Height=76,Padding=new Padding(14,7,14,5),BackColor=panel};
  stats.Dock=DockStyle.Top;stats.Height=21;stats.ForeColor=accent;stats.Font=new Font("Segoe UI",9,FontStyle.Bold);
  status.Dock=DockStyle.Fill;status.AutoEllipsis=true;status.ForeColor=fg;status.Text="Guardado incremental: cada 5 s o 4 MiB por conexión. Datos sincronizados antes del registro.\nCerrar esta ventana mantiene las descargas en la bandeja. Salir detiene el programa sin borrar el avance.";
  footer.Controls.Add(status);footer.Controls.Add(stats);Controls.Add(footer);
  grid.Dock=DockStyle.Fill;grid.BackgroundColor=bg;grid.BorderStyle=BorderStyle.None;grid.AllowUserToAddRows=false;grid.AllowUserToDeleteRows=false;grid.ReadOnly=true;grid.RowHeadersVisible=false;grid.SelectionMode=DataGridViewSelectionMode.FullRowSelect;grid.MultiSelect=false;grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.EnableHeadersVisualStyles=false;grid.ColumnHeadersDefaultCellStyle.BackColor=panel;grid.ColumnHeadersDefaultCellStyle.ForeColor=fg;grid.ColumnHeadersDefaultCellStyle.Padding=new Padding(2);grid.ColumnHeadersHeight=28;grid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;grid.RowTemplate.Height=31;grid.AllowUserToResizeRows=false;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.Single;grid.DefaultCellStyle.BackColor=bg;grid.DefaultCellStyle.ForeColor=fg;grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(42,75,79);grid.DefaultCellStyle.SelectionForeColor=Color.White;grid.DefaultCellStyle.Padding=new Padding(2);grid.AlternatingRowsDefaultCellStyle.BackColor=Color.FromArgb(24,30,41);grid.GridColor=panel;
  foreach(string c in new[]{"Archivo","Estado","Guardado","Tamaño","Velocidad","Restante"})grid.Columns.Add(c,c);grid.Columns[0].AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;
  int[] widths={108,115,60,76,82};for(int i=1;i<6;i++){grid.Columns[i].AutoSizeMode=DataGridViewAutoSizeColumnMode.None;grid.Columns[i].Width=widths[i-1];grid.Columns[i].Resizable=DataGridViewTriState.False;}
  grid.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Delete){e.Handled=true;RemoveSelected();}};
  var rowMenu=new ContextMenuStrip();rowMenu.Items.Add("Quitar de la lista",null,(s,e)=>RemoveSelected());rowMenu.Items.Add("Limpiar terminadas",null,(s,e)=>ClearCompleted());grid.ContextMenuStrip=rowMenu;
  grid.CellMouseDown+=(s,e)=>{if(e.Button==MouseButtons.Right&&e.RowIndex>=0){grid.ClearSelection();grid.Rows[e.RowIndex].Selected=true;}};

  grid.CellDoubleClick+=(s,e)=>Details();grid.SelectionChanged+=(s,e)=>UpdateDetail();Controls.Add(grid);grid.BringToFront();
  var menu=new ContextMenuStrip();menu.Items.Add("Abrir Drex Downloads",null,(s,e)=>Restore());menu.Items.Add("Nueva descarga",null,(s,e)=>{Restore();AddDownload();});menu.Items.Add("Salir y conservar progreso",null,(s,e)=>Exit());
  tray=new NotifyIcon{Icon=Icon,Text="Drex Downloads",Visible=true,ContextMenuStrip=menu};tray.DoubleClick+=(s,e)=>Restore();
  var options=new ToolStripMenuItem("Iniciar con Windows"){Checked=File.Exists(StartupFile),CheckOnClick=true};options.Click+=(s,e)=>SetStartup(options.Checked);menu.Items.Insert(2,options);
  timer=new System.Windows.Forms.Timer{Interval=1000};timer.Tick+=(s,e)=>{RefreshGrid();NetworkLease();string request=Path.Combine(root,"show.request");if(File.Exists(request)){try{File.Delete(request);}catch{}Restore();}};timer.Start();
  Shown+=(s,e)=>{RefreshGrid();if(exclusive.Checked || File.Exists(Path.Combine(root,"network","snapshot.json"))){try{NetworkLease();NetworkControl.Ensure();}catch(Exception ex){exclusive.Checked=false;MessageBox.Show(ex.Message);}}if(!string.IsNullOrEmpty(engine.StartupWarning))MessageBox.Show(this,engine.StartupWarning,"Recuperación");};
  FormClosing+=(s,e)=>{if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();tray.ShowBalloonTip(2500,"Drex Downloads","Sigo descargando en segundo plano. Puedes abrirme desde la bandeja.",ToolTipIcon.Info);}else{timer.Stop();NetworkLease(true);engine.Dispose();tray.Visible=false;tray.Dispose();}};
  AddTitleBar();
  engine.Start();
 }
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
 void AddTitleBar(){
  var caption=new TexturePanel{Dock=DockStyle.Top,Height=34,BackColor=panel,AccessibleName="Barra de título"};
  var mark=new PictureBox{Image=Icon.ToBitmap(),SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(12,7),Size=new Size(20,20),BackColor=Color.Transparent};
  var name=new Label{Text=Text,AutoSize=false,Location=new Point(40,0),Size=new Size(590,34),TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.Transparent,ForeColor=fg};
  MouseEventHandler drag=(o,e)=>{if(e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}};
  caption.MouseDown+=drag;name.MouseDown+=drag;
  var minimize=new Button{Text="—",AccessibleName="Minimizar",FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=fg,Location=new Point(668,0),Size=new Size(46,34),TabStop=false};
  var close=new Button{Text="×",AccessibleName="Cerrar a la bandeja",FlatStyle=FlatStyle.Flat,BackColor=panel,ForeColor=fg,Location=new Point(714,0),Size=new Size(46,34),Font=new Font("Segoe UI",14),TabStop=false};
  minimize.FlatAppearance.BorderSize=0;close.FlatAppearance.BorderSize=0;close.FlatAppearance.MouseOverBackColor=Color.FromArgb(170,48,58);
  minimize.Click+=(o,e)=>WindowState=FormWindowState.Minimized;close.Click+=(o,e)=>Close();
  caption.Controls.AddRange(new Control[]{mark,name,minimize,close});Controls.Add(caption);
 }
 void NetworkLease(bool stop=false){try{string dir=Path.Combine(root,"network");Directory.CreateDirectory(dir);bool active;lock(engine.Gate)active=engine.Jobs.Any(j=>j.D.Desired&&!j.D.Completed&&!j.D.Removed);Store.Atomic(Path.Combine(dir,"lease.json"),Encoding.UTF8.GetBytes("{\"enabled\":"+(exclusive.Checked&&!stop?"true":"false")+",\"active\":"+(active&&!stop?"true":"false")+"}"));string f=Path.Combine(dir,"status.txt");networkState.Text=File.Exists(f)?File.ReadAllText(f):"Internet normal · LAN libre";if(exclusive.Checked&&File.Exists(f)&&(DateTime.UtcNow-File.GetLastWriteTimeUtc(f)).TotalSeconds>20)networkState.Text="Supervisor sin respuesta";}catch{networkState.Text="Error del control de red";}}
 string StartupFile {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"Drex Downloads.vbs");}}
 void SetStartup(bool enable){try{if(enable){string exe=Application.ExecutablePath.Replace("\"","\"\"");File.WriteAllText(StartupFile,"CreateObject(\"WScript.Shell\").Run Chr(34) & \""+exe+"\" & Chr(34), 1, False\r\n");}else if(File.Exists(StartupFile))File.Delete(StartupFile);}catch(Exception ex){MessageBox.Show(ex.Message);}}
 Button Button(string text,Action action,bool primary=false){var b=new SoftButton{Text=text,AutoSize=false,Height=34,Width=TextRenderer.MeasureText(text,Font).Width+24,FlatStyle=FlatStyle.Flat,BackColor=primary?accent:panel,ForeColor=primary?bg:fg,Margin=new Padding(3,0,3,0),Padding=new Padding(4,0,4,0)};b.FlatAppearance.BorderSize=0;b.Click+=(s,e)=>{try{action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Drex Downloads",MessageBoxButtons.OK,MessageBoxIcon.Information);}};return b;}
 void Restore(){Show();WindowState=FormWindowState.Normal;Activate();}
 void Exit(){exiting=true;Close();}
 Job Selected(){return grid.SelectedRows.Count>0?grid.SelectedRows[0].Tag as Job:null;}
 void SafeAct(Action<Job> f){try{Act(f);}catch(Exception ex){MessageBox.Show(this,ex.Message,"Drex Downloads");}}
 void Act(Action<Job> f){var j=Selected();if(j==null)throw new NeedAction("Selecciona una descarga.");f(j);RefreshGrid();}
 void AddDownload(){using(var f=new AddForm(root)){if(f.ShowDialog(this)==DialogResult.OK){var urls=f.Urls.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries);int count=0;var failures=new List<string>();foreach(string u in urls){try{engine.Add(u,f.Folder,urls.Length==1?f.FileName:"",urls.Length==1?f.Hash:"",f.Connections,f.Limit,f.Token,f.StartNow);count++;}catch(Exception ex){failures.Add("Línea "+(count+failures.Count+1)+": "+ex.Message);}}RefreshGrid();stats.Text=count+" descarga(s) añadida(s)";if(failures.Count>0)MessageBox.Show(this,count+" añadidas. Revisa:\n"+string.Join("\n",failures),"Añadir descargas");}}}
 void Renew(){Act(j=>{if(j.Working)throw new NeedAction("Pulsa Pausar y espera antes de renovar.");string url=Prompt("Renovar enlace","Pega el enlace nuevo del MISMO archivo. Se comprobará la identidad antes de continuar.","");if(url!=null)engine.Renew(j,url);});}
 async void RemoveSelected(){
 var j=Selected();if(j==null){MessageBox.Show(this,"Selecciona una descarga.","Quitar de la lista");return;}
 if(MessageBox.Show(this,"Quitar de la lista: "+j.D.Name+"\n\nSi está descargando, se detendrá primero. Se conservan los archivos y las partes guardadas en el disco.","Quitar descarga",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
 try{engine.Pause(j);while(j.Working&&!exiting)await System.Threading.Tasks.Task.Delay(100);if(exiting)return;engine.Remove(j);status.Text="Descarga retirada. Archivos conservados en el disco.";RefreshGrid();}catch(Exception ex){MessageBox.Show(this,ex.Message,"Quitar descarga");}
 }
 void ClearCompleted(){List<Job> done;lock(engine.Gate)done=engine.Jobs.Where(j=>j.D.Completed&&!j.Working).ToList();if(done.Count==0)return;if(MessageBox.Show(this,"Quitar "+done.Count+" descargas terminadas de la lista. Los archivos NO se borran.","Limpiar terminadas",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;foreach(var j in done)engine.Remove(j);status.Text="Terminadas retiradas. Archivos conservados.";RefreshGrid();}
 void OpenFolder(){Act(j=>{if(Directory.Exists(j.D.Folder))Process.Start("explorer.exe",j.D.Completed?"/select,\""+j.D.Destination+"\"":"\""+j.D.Folder+"\"");});}
 void More(){var m=new ContextMenuStrip();m.Items.Add("Detalles y SHA-256",null,(s,e)=>Details());m.Items.Add("Cambiar SHA-256 esperado",null,(s,e)=>{try{Act(j=>{if(j.Working||j.D.Completed)throw new NeedAction("Pausa una descarga pendiente antes de editarla.");string v=Prompt("SHA-256 esperado","Pega el SHA-256 del proveedor (64 caracteres). Vacío = sin referencia externa.",j.D.ExpectedHash??"");if(v==null)return;if(v.Length>0&&!System.Text.RegularExpressions.Regex.IsMatch(v.Trim(),"^[a-fA-F0-9]{64}$"))throw new NeedAction("SHA-256 inválido.");j.D.ExpectedHash=v.Trim().ToLowerInvariant();j.D.ActualHash=null;engine.Save(j);});}catch(Exception ex){MessageBox.Show(ex.Message);}});m.Items.Add("Subir en la cola",null,(s,e)=>SafeAct(j=>engine.Move(j,-1)));m.Items.Add("Bajar en la cola",null,(s,e)=>SafeAct(j=>engine.Move(j,1)));m.Items.Add("Quitar descarga seleccionada",null,(s,e)=>RemoveSelected());m.Items.Add("Limpiar todas las terminadas",null,(s,e)=>ClearCompleted());m.Items.Add("Límite global de velocidad",null,(s,e)=>{string v=Prompt("Velocidad","Límite global en KiB/s. 0 = sin límite.",engine.GlobalLimitKiB.ToString());int n;if(v!=null&&int.TryParse(v,out n)&&n>=0)engine.GlobalLimitKiB=n;});m.Items.Add("Guía de uso",null,(s,e)=>Process.Start(Path.Combine(root,"LEEME.txt")));m.Items.Add("Salir y conservar progreso",null,(s,e)=>Exit());m.Show(Cursor.Position);}
 void Details(){var j=Selected();if(j==null)return;var d=j.D;MessageBox.Show(this,"Archivo: "+d.Name+"\nDestino: "+d.Folder+"\nEstado: "+d.State+"\nGuardado confirmado: "+SizeText(d.Saved)+"\nÚltimo punto guardado: "+(d.LastCheckpoint==default(DateTime)?"Pendiente":d.LastCheckpoint.ToLocalTime().ToString())+"\nSHA-256 calculado: "+(d.ActualHash??"Pendiente")+"\nSHA-256 esperado: "+(string.IsNullOrEmpty(d.ExpectedHash)?"No proporcionado; no hay verificación externa":d.ExpectedHash)+"\n\n"+d.Error,"Detalles de la descarga");}
 public static string SizeText(long b){if(b<0)return "—";string[] u={"B","KiB","MiB","GiB","TiB"};double n=b;int i=0;while(n>=1024&&i<u.Length-1){n/=1024;i++;}return n.ToString(i==0?"0":"0.0")+" "+u[i];}
 void RefreshGrid(){if(IsDisposed)return;var selected=Selected();string id=selected==null?null:selected.D.Id;List<Job> jobs;lock(engine.Gate)jobs=engine.Jobs.ToList();double sec=Math.Max(.1,(DateTime.UtcNow-tick).TotalSeconds);tick=DateTime.UtcNow;
  grid.SuspendLayout();grid.Rows.Clear();foreach(var j in jobs){long old;previous.TryGetValue(j.D.Id,out old);long current=Interlocked.Read(ref j.NetworkBytes);double speed=j.Working?Math.Max(0,current-old)/sec:0;previous[j.D.Id]=current;j.Speed=j.Working?(j.Speed*.6+speed*.4):0;if((DateTime.UtcNow.Ticks-Interlocked.Read(ref j.LastReceiveTicks))/10000000.0>10)j.Speed=0;speed=j.Speed;if(!j.D.Name.ToLowerInvariant().Contains(filter.Text.ToLowerInvariant()))continue;
   double percent=j.D.Size>0?100.0*j.D.Saved/j.D.Size:0;string eta=speed>0&&j.D.Size>j.D.Saved?TimeSpan.FromSeconds(Math.Min(86400000,(j.D.Size-j.D.Saved)/speed)).ToString(@"d\.hh\:mm\:ss"):"—";
   string state=j.D.State;if(j.Working&&state=="Descargando"){double idle=(DateTime.UtcNow.Ticks-Interlocked.Read(ref j.LastReceiveTicks))/10000000.0;if(j.ActiveCommits>0)state="Guardando en disco";else if(idle>=5)state="Esperando datos · "+((int)idle)+" s";}
   int row=grid.Rows.Add(j.D.Name,state,SizeText(j.D.Saved)+(j.D.Size>0?" · "+percent.ToString("0.0")+"%":""),SizeText(j.D.Size),speed>0?SizeText((long)speed)+"/s":"—",eta);grid.Rows[row].Tag=j;if(j.D.Id==id)grid.Rows[row].Selected=true;
  }if(id==null&&grid.Rows.Count>0)grid.ClearSelection();grid.ResumeLayout();stats.Text=jobs.Count+" archivos  ·  "+jobs.Count(j=>j.D.Completed)+" completados  ·  "+jobs.Count(j=>j.D.Desired&&!j.D.Completed)+" en cola o activos";UpdateDetail();
 }
 void UpdateDetail(){var j=Selected();if(j!=null)status.Text=j.D.Name+"\n"+(string.IsNullOrEmpty(j.D.Error)?(j.D.Completed?(string.IsNullOrEmpty(j.D.ExpectedHash)?"Descarga completa. Huella SHA-256 calculada; sin hash externo para comparar.":"Descarga completa y SHA-256 esperado verificado."):"Guardado confirmado: "+SizeText(j.D.Saved)+". Último punto: "+(j.D.LastCheckpoint==default(DateTime)?"pendiente":j.D.LastCheckpoint.ToLocalTime().ToString("HH:mm:ss"))):j.D.Error);}
 public static string Prompt(string title,string message,string value){using(var f=new Form{Text=title,Width=680,Height=230,StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false}){var l=new Label{Text=message,Left=20,Top=20,Width=620,Height=50};var t=new TextBox{Text=value,Left=20,Top=80,Width=620};var ok=new Button{Text="Aceptar",DialogResult=DialogResult.OK,Left=430,Top=125,Width=100};var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel,Left=540,Top=125,Width=100};f.Controls.AddRange(new Control[]{l,t,ok,cancel});f.AcceptButton=ok;f.CancelButton=cancel;return f.ShowDialog()==DialogResult.OK?t.Text:null;}}
}
public class AddForm:Form {
 TextBox urls=new TextBox(),folder=new TextBox(),name=new TextBox(),hash=new TextBox(),token=new TextBox();NumericUpDown connections=new NumericUpDown(),limit=new NumericUpDown();CheckBox start=new CheckBox();
 public string Urls{get{return urls.Text;}}public string Folder{get{return folder.Text;}}public string FileName{get{return name.Text;}}public string Hash{get{return hash.Text;}}public string Token{get{return token.Text;}}public int Connections{get{return (int)connections.Value;}}public int Limit{get{return (int)limit.Value;}}public bool StartNow{get{return start.Checked;}}
 public AddForm(string root){Text="Nueva descarga · Drex Downloads";Width=760;Height=640;StartPosition=FormStartPosition.CenterParent;Font=new Font("Segoe UI",10);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
  LabelAt("Enlace directo HTTP/HTTPS (uno por línea para añadir varios)",20);urls.SetBounds(22,46,695,100);urls.Multiline=true;urls.ScrollBars=ScrollBars.Vertical;Controls.Add(urls);
  LabelAt("Guardar en",158);folder.SetBounds(22,183,585,28);folder.Text=Path.Combine(root,"Descargas");Controls.Add(folder);var browse=new Button{Text="Elegir…",Left=617,Top=181,Width=100,Height=30};browse.Click+=(s,e)=>{using(var f=new FolderBrowserDialog{SelectedPath=folder.Text})if(f.ShowDialog()==DialogResult.OK)folder.Text=f.SelectedPath;};Controls.Add(browse);
  LabelAt("Nombre opcional (solo para un enlace)",225);name.SetBounds(22,250,695,28);Controls.Add(name);
  LabelAt("SHA-256 esperado, opcional (permite comprobar el archivo original)",289);hash.SetBounds(22,314,695,28);Controls.Add(hash);
  LabelAt("Token de Hugging Face, opcional (se guarda cifrado para tu usuario Windows)",352);token.SetBounds(22,377,695,28);token.UseSystemPasswordChar=true;Controls.Add(token);
  LabelAt("Conexiones (1–4)",421);connections.SetBounds(22,447,110,28);connections.Minimum=1;connections.Maximum=4;connections.Value=2;Controls.Add(connections);
  var speed=new Label{Text="Límite KiB/s (0 = sin límite)",Left=190,Top=421,AutoSize=true};Controls.Add(speed);limit.SetBounds(190,447,140,28);limit.Maximum=1000000;Controls.Add(limit);
  start.Text="Iniciar al añadir (una descarga de la cola a la vez)";start.SetBounds(22,492,650,28);start.Checked=true;Controls.Add(start);
  var ok=new Button{Text="Añadir descarga",Left=430,Top=541,Width=160,Height=36};ok.Click+=(s,e)=>{if(string.IsNullOrWhiteSpace(urls.Text)||string.IsNullOrWhiteSpace(folder.Text)){MessageBox.Show("Escribe el enlace y la carpeta.");return;}DialogResult=DialogResult.OK;};var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel,Left=603,Top=541,Width=114,Height=36};Controls.AddRange(new Control[]{ok,cancel});AcceptButton=ok;CancelButton=cancel;
 }
 void LabelAt(string s,int y){Controls.Add(new Label{Text=s,Left=22,Top=y,AutoSize=true});}
}
}
