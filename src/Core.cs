using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DrexDownloads {
[DataContract] public class JobData {
 [DataMember] public string Id, Url, Name, Folder, ExpectedHash, ActualHash, ETag, LastModified, PinnedCommit, State, Error, TokenProtected;
 [DataMember] public long Size=-1, Saved, Generation, Order;
 [DataMember] public bool Desired=true, Completed=false, Removed=false;
 [DataMember] public int Connections=2, LimitKiB=0;
 [DataMember] public DateTime Created=DateTime.UtcNow, LastCheckpoint;
 public string Destination { get { return Path.Combine(Folder,Name); } }
 public string Work { get { return Path.Combine(Folder,".drex-partials",Id); } }
 public string Part { get { return Path.Combine(Work,"download.part"); } }
}
[DataContract] public class Block {
 [DataMember] public long Offset;
 [DataMember] public int Length;
 [DataMember] public string Hash;
}
[DataContract] class Envelope {
 [DataMember] public string Payload,Hash;
}
public class RetryLater : WebException { public int Seconds,Code; public RetryLater(int code,int seconds):base("Servidor ocupado"){Code=code;Seconds=seconds;} }
public class NeedAction : Exception { public NeedAction(string s):base(s){} }
public static class Store {
 [DllImport("kernel32.dll",SetLastError=true)] static extern bool DeviceIoControl(SafeFileHandle h,uint code,IntPtr input,uint inputLength,IntPtr output,uint outputLength,out uint returned,IntPtr overlapped);
 public static bool EnableSparse(FileStream f){if(Environment.OSVersion.Platform!=PlatformID.Win32NT)return false;uint n;return DeviceIoControl(f.SafeFileHandle,0x000900c4,IntPtr.Zero,0,IntPtr.Zero,0,out n,IntPtr.Zero);}

 public static byte[] Serialize<T>(T value) { using(var m=new MemoryStream()){new DataContractJsonSerializer(typeof(T)).WriteObject(m,value);return m.ToArray();} }
 public static T Deserialize<T>(byte[] bytes) { using(var m=new MemoryStream(bytes)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(m); }
 public static string Hash(byte[] b){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
 public static string HashFile(string p){using(var s=File.OpenRead(p))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
 public static byte[] Wrap<T>(T value) { byte[] b=Serialize(value);return Serialize(new Envelope{Payload=Convert.ToBase64String(b),Hash=Hash(b)}); }
 public static T Unwrap<T>(byte[] b) {var e=Deserialize<Envelope>(b);var p=Convert.FromBase64String(e.Payload);if(Hash(p)!=e.Hash)throw new IOException("Registro incompleto");return Deserialize<T>(p);}
 public static void DurableWrite(string p, byte[] b) {using(var f=new FileStream(p,FileMode.Create,FileAccess.Write,FileShare.None)){f.Write(b,0,b.Length);f.Flush(true);}}
 public static void Atomic(string path,byte[] b){string t=path+".new";DurableWrite(t,b);if(File.Exists(path))File.Replace(t,path,null);else File.Move(t,path);}
 public static void Append(string path,byte[] b){using(var f=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read)){f.Write(b,0,b.Length);f.WriteByte(10);f.Flush(true);}}
 public static string SafeName(string s) {s=Path.GetFileName(s.Trim()).TrimEnd('.',' ');foreach(char c in Path.GetInvalidFileNameChars())s=s.Replace(c,'_');if(string.IsNullOrWhiteSpace(s))s="descarga.bin";if(Regex.IsMatch(s,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",RegexOptions.IgnoreCase))s="_"+s;if(s.Length>150)s=s.Substring(0,120)+Path.GetExtension(s).Substring(0,Math.Min(20,Path.GetExtension(s).Length));return s;}
 public static string Protect(string s) {if(string.IsNullOrEmpty(s))return null;return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s),null,DataProtectionScope.CurrentUser));}
 public static string Unprotect(string s) {if(string.IsNullOrEmpty(s))return null;return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(s),null,DataProtectionScope.CurrentUser));}
}
public class Job {
 public JobData D;
 public volatile bool Stop, Working;
 public bool SupportsRanges;
 public long NetworkBytes;
 public long LastReceiveTicks=DateTime.UtcNow.Ticks;
 public int ActiveCommits;
 public double Speed;
 public int Attempts;
 public readonly object Gate=new object();
 public List<Block> Blocks=new List<Block>();
 public Job(JobData d){D=d;}
}
public class Engine : IDisposable {
 public readonly string Root;
 public readonly List<Job> Jobs=new List<Job>();
 public readonly object Gate=new object();
 public volatile bool Closing;
 public int GlobalLimitKiB=0;
 Thread scheduler;
 public string StartupWarning="";
 public void Record(Job j){try{string p=Path.Combine(Root,"events.log");if(File.Exists(p)&&new FileInfo(p).Length>1024*1024){string backup=p+".previous";if(File.Exists(backup))File.Delete(backup);File.Move(p,backup);}File.AppendAllText(p,DateTime.UtcNow.ToString("o")+" | "+j.D.Id+" | "+j.D.State+" | "+(j.D.Error??"").Replace("\r"," ").Replace("\n"," ")+Environment.NewLine);}catch{}}
 public Engine(string root) {
  Root=Path.GetFullPath(root);Directory.CreateDirectory(Path.Combine(Root,"jobs"));
  ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
  ServicePointManager.DefaultConnectionLimit=16;
  Load();
 }
 string Meta(Job j,int slot){return Path.Combine(Root,"jobs",j.D.Id+"."+slot+".json");}
 public void Save(Job j){lock(j.Gate){j.D.Generation++;byte[] b=Store.Wrap(j.D);Store.Atomic(Meta(j,(int)(j.D.Generation%2)),b);}}
 void Load(){
  var ids=Directory.GetFiles(Path.Combine(Root,"jobs"),"*.json").Select(p=>Path.GetFileName(p).Split('.')[0]).Distinct();
  foreach(string id in ids){JobData best=null;foreach(int slot in new[]{0,1})try{var d=Store.Unwrap<JobData>(File.ReadAllBytes(Path.Combine(Root,"jobs",id+"."+slot+".json")));if(best==null||d.Generation>best.Generation)best=d;}catch{}
   if(best==null){StartupWarning="Hay metadatos dañados. Se conservaron para revisión; no se borraron parciales.";continue;}
   if(best.Removed)continue;
   if(!best.Completed){best.State=best.Desired?"En cola":"Pausado";best.Error="";}
   Jobs.Add(new Job(best));
  }Jobs.Sort((a,b)=>a.D.Order.CompareTo(b.D.Order));
 }
 public Job Add(string url,string folder,string name,string hash,int connections,int limit,string token,bool start) {
  Uri u;if(!Uri.TryCreate(url.Trim(),UriKind.Absolute,out u)||(u.Scheme!="http"&&u.Scheme!="https"))throw new NeedAction("Usa un enlace HTTP o HTTPS directo.");
  if(!string.IsNullOrEmpty(u.UserInfo))throw new NeedAction("No incluyas contraseñas en la dirección.");
  if(!string.IsNullOrWhiteSpace(hash)&&!Regex.IsMatch(hash.Trim(),"^[a-fA-F0-9]{64}$"))throw new NeedAction("El SHA-256 debe tener 64 caracteres hexadecimales.");
  folder=Path.GetFullPath(folder.Trim());Directory.CreateDirectory(folder);
  if(string.IsNullOrWhiteSpace(name))name=Uri.UnescapeDataString(u.AbsolutePath.Split('/').Last());
  name=Store.SafeName(name);
  if(Path.Combine(folder,name).Length>240)throw new NeedAction("Elige una carpeta o nombre más corto (máximo 240 caracteres en esta versión).");
  lock(Gate){
   var duplicate=Jobs.FirstOrDefault(j=>j.D.Url==u.AbsoluteUri&&!j.D.Completed&&!j.D.Removed);if(duplicate!=null)throw new NeedAction("Ese enlace ya está en la lista.");
   string original=name;int n=1;while(File.Exists(Path.Combine(folder,name))||Jobs.Any(j=>j.D.Folder.Equals(folder,StringComparison.OrdinalIgnoreCase)&&j.D.Name.Equals(name,StringComparison.OrdinalIgnoreCase)))name=Path.GetFileNameWithoutExtension(original)+" ("+(n++)+")"+Path.GetExtension(original);
   var d=new JobData{Id=Guid.NewGuid().ToString("N"),Url=u.AbsoluteUri,Name=name,Folder=folder,ExpectedHash=(hash??"").Trim().ToLowerInvariant(),Connections=Math.Max(1,Math.Min(4,connections)),LimitKiB=Math.Max(0,limit),TokenProtected=Store.Protect(token),State=start?"En cola":"Pausado",Desired=start,Order=DateTime.UtcNow.Ticks};
   var job=new Job(d);Directory.CreateDirectory(d.Work);Save(job);Save(job);Jobs.Add(job);return job;
  }
 }
 public void Start(){scheduler=new Thread(Schedule){IsBackground=true};scheduler.Start();}
 void Schedule(){while(!Closing){Job job=null;lock(Gate)job=Jobs.FirstOrDefault(j=>j.D.Desired&&!j.D.Completed&&!j.D.Removed&&!j.Working);if(job==null){Thread.Sleep(200);continue;}job.Stop=false;job.Working=true;long savedBefore=job.D.Saved;try{Run(job);}catch(OperationCanceledException){job.D.State=job.D.Desired?"En cola":"Pausado";}catch(NeedAction ex){job.D.Error=ex.Message;job.D.State="Requiere atención";job.D.Desired=false;}catch(WebException ex){job.D.Error=NetworkError(ex);job.D.State="Esperando red";job.Attempts=job.D.Saved>savedBefore?1:job.Attempts+1;if(job.Attempts>=5){job.D.Desired=false;job.D.State="Requiere atención";}else { if(job.D.Desired&&!Closing)job.Stop=false; int delay=Math.Min(30,job.Attempts*3);var busy=ex as RetryLater;if(busy!=null)delay=Math.Max(delay,busy.Seconds);for(int i=0;i<delay*10&&!job.Stop&&!Closing;i++)Thread.Sleep(100); }}catch(IOException){job.D.Error="No se pudo leer o escribir. Revisa espacio, unidad y permisos. Tus parciales se conservan.";job.D.Desired=false;job.D.State="Requiere atención";}catch(Exception){job.D.Error="La tarea se detuvo de forma segura. Los parciales se conservan; prueba Continuar.";job.D.Desired=false;job.D.State="Requiere atención";}finally{job.Speed=0;job.Working=false;try{Save(job);Record(job);}catch{} }} }
 string NetworkError(WebException ex){var busy=ex as RetryLater;if(busy!=null)return "El servidor pidió esperar (HTTP "+busy.Code+"). Se respetará el intervalo indicado.";var r=ex.Response as HttpWebResponse;if(r!=null){int code=(int)r.StatusCode;r.Close();return "El servidor respondió HTTP "+code+". Revisa el enlace o la autorización.";}return "La conexión se interrumpió. Reintento automático conservando el avance.";}
 public void Pause(Job j){j.D.Desired=false;j.Stop=true;j.D.State=j.Working?"Pausando":"Pausado";Save(j);}
 public void Resume(Job j){if(j.Working)throw new NeedAction("Espera a que termine de pausar.");if(j.D.Completed)return;j.Stop=false;j.Attempts=0;j.D.Error="";j.D.State="En cola";j.D.Desired=true;Save(j);}
 public void Renew(Job j,string url){if(j.Working)throw new NeedAction("Pausa la tarea antes de renovar el enlace.");Uri u;if(!Uri.TryCreate(url.Trim(),UriKind.Absolute,out u)||(u.Scheme!="http"&&u.Scheme!="https")||!string.IsNullOrEmpty(u.UserInfo))throw new NeedAction("Enlace HTTP/HTTPS inválido.");j.D.Url=u.AbsoluteUri;j.D.Error="";Save(j);Resume(j);}
 public void Move(Job j,int direction){lock(Gate){int i=Jobs.IndexOf(j),n=i+direction;if(n<0||n>=Jobs.Count)return;Jobs.RemoveAt(i);Jobs.Insert(n,j);for(int k=0;k<Jobs.Count;k++){Jobs[k].D.Order=k;Save(Jobs[k]);}}}
 public void Remove(Job j){if(j.Working)throw new NeedAction("Pausa la tarea antes de retirarla.");j.D.Removed=true;j.D.Desired=false;Save(j);lock(Gate)Jobs.Remove(j);}
 void Check(Job j){if(j.Stop||Closing)throw new OperationCanceledException();}
 HttpWebRequest Request(Job j,long from,long to,bool ranged) {
  var r=(HttpWebRequest)WebRequest.Create(j.D.Url);r.UserAgent="DrexDownloads/1.0.1";r.Timeout=30000;r.ReadWriteTimeout=60000;r.AllowAutoRedirect=true;r.MaximumAutomaticRedirections=8;r.AutomaticDecompression=DecompressionMethods.None;r.Headers[HttpRequestHeader.AcceptEncoding]="identity";
  if(ranged){r.AddRange(from,to);if(!string.IsNullOrEmpty(j.D.ETag)&&!j.D.ETag.StartsWith("W/"))r.Headers[HttpRequestHeader.IfRange]=j.D.ETag;}
  if(r.RequestUri.Scheme=="https"&&r.RequestUri.Host.Equals("huggingface.co",StringComparison.OrdinalIgnoreCase)){string token=Store.Unprotect(j.D.TokenProtected);if(!string.IsNullOrEmpty(token))r.Headers[HttpRequestHeader.Authorization]="Bearer "+token;}
  return r;
 }
 HttpWebResponse Response(HttpWebRequest r){try{return (HttpWebResponse)r.GetResponse();}catch(WebException ex){var resp=ex.Response as HttpWebResponse;if(resp!=null){int code=(int)resp.StatusCode;string retry=resp.Headers["Retry-After"];resp.Close();if(code==429||code==503){int seconds=0;DateTime until;if(!int.TryParse(retry,out seconds)&&DateTime.TryParse(retry,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AdjustToUniversal,out until))seconds=(int)Math.Max(0,(until.ToUniversalTime()-DateTime.UtcNow).TotalSeconds);throw new RetryLater(code,Math.Max(0,Math.Min(86400,seconds)));}if(code==401||code==403)throw new NeedAction("Enlace caducado o acceso denegado ("+code+"). Renueva el enlace o revisa el token.");if(code==404||code==410)throw new NeedAction("El archivo no está disponible ("+code+"). Renueva el enlace.");if(code==416)throw new NeedAction("El servidor rechazó el rango. Se conserva el progreso; no se reiniciará desde cero.");}throw;}}
 long ParseRange(HttpWebResponse r,long start,long? end) {var m=Regex.Match(r.Headers["Content-Range"]??"",@"^bytes (\d+)-(\d+)/(\d+)$");long a,b,t;if(!m.Success||!long.TryParse(m.Groups[1].Value,out a)||!long.TryParse(m.Groups[2].Value,out b)||!long.TryParse(m.Groups[3].Value,out t)||a!=start||b<a||b>=t||(end.HasValue&&b!=end.Value))throw new NeedAction("Respuesta parcial inválida. No se modificaron bloques confirmados.");return t;}
 void Probe(Job j) {
  j.D.State="Comprobando origen";Check(j);
  // HEAD keeps the provider metadata before CDN redirects; pin HF to immutable revision when available.
  var uri=new Uri(j.D.Url);
  if(uri.Host.Equals("huggingface.co",StringComparison.OrdinalIgnoreCase)&&uri.AbsolutePath.Contains("/resolve/")){
   var head=Request(j,0,0,false);head.Method="HEAD";head.AllowAutoRedirect=false;
   try{using(var hr=(HttpWebResponse)head.GetResponse()){string commit=hr.Headers["X-Repo-Commit"];if(!string.IsNullOrEmpty(commit)&&Regex.IsMatch(commit,"^[a-fA-F0-9]{40}$")){if(!string.IsNullOrEmpty(j.D.PinnedCommit)&&j.D.PinnedCommit!=commit)throw new NeedAction("La revisión de Hugging Face cambió. Se conservan tus partes.");j.D.PinnedCommit=commit;var ub=new UriBuilder(j.D.Url);ub.Path=Regex.Replace(uri.AbsolutePath,@"/resolve/[^/]+/","/resolve/"+commit+"/");j.D.Url=ub.Uri.AbsoluteUri;}}}catch(WebException ex){if(ex.Response!=null)ex.Response.Close();/* regular GET below reports failures */}
  }
  var req=Request(j,0,0,true);using(var r=Response(req)){
   if(Regex.IsMatch(j.D.Name,"^[0-9a-fA-F]{64}$")){
    string disposition=r.Headers["Content-Disposition"]??"";var named=Regex.Match(disposition,@"filename\*\s*=\s*UTF-8''([^;]+)",RegexOptions.IgnoreCase);
    string candidate=named.Success?Uri.UnescapeDataString(named.Groups[1].Value.Trim().Trim('"')):null;
    if(candidate==null){var basic=Regex.Match(disposition,"filename\\s*=\\s*\"([^\"]+)\"",RegexOptions.IgnoreCase);if(basic.Success)candidate=basic.Groups[1].Value;}
    if(!string.IsNullOrWhiteSpace(candidate)){candidate=Store.SafeName(candidate);lock(Gate){if(!File.Exists(Path.Combine(j.D.Folder,candidate))&&!Jobs.Any(other=>other!=j&&other.D.Folder==j.D.Folder&&other.D.Name==candidate))j.D.Name=candidate;}}
   }
   string extension=Path.GetExtension(j.D.Name).ToLowerInvariant();string mime=(r.ContentType??"").ToLowerInvariant();if(new[]{".safetensors",".pt",".ckpt",".bin",".gguf",".onnx",".zip",".7z",".exe",".mp4",".wav"}.Contains(extension)&&(mime.Contains("text/html")||mime.Contains("application/json")))throw new NeedAction("El servidor devolvió una página o un mensaje, no el archivo binario esperado. Revisa el enlace o la sesión.");
   long total;if(r.StatusCode==HttpStatusCode.PartialContent)total=ParseRange(r,0,0);else if(r.StatusCode==HttpStatusCode.OK)total=r.ContentLength;else throw new NeedAction("El servidor no entregó un archivo descargable.");
   if(total<0)throw new NeedAction("El servidor no informa el tamaño. Esta versión requiere tamaño conocido.");
   string etag=r.Headers["ETag"];
   bool existing=j.D.Size>=0;
   if(existing&&j.D.Size!=total)throw new NeedAction("El tamaño remoto cambió. No se mezclarán archivos distintos.");
   if(j.Blocks.Count>0){
    bool sameStrong=!string.IsNullOrEmpty(j.D.ETag)&&!j.D.ETag.StartsWith("W/")&&j.D.ETag==etag;
    bool expected=!string.IsNullOrEmpty(j.D.ExpectedHash);
    if(!sameStrong&&!expected)throw new NeedAction("No hay una identidad remota estable para reanudar con seguridad. Conservamos el parcial. Necesitas el mismo ETag o un SHA-256 esperado.");
    if(r.StatusCode!=HttpStatusCode.PartialContent)throw new NeedAction("Este servidor no permite reanudar por partes. Tu avance permanece guardado.");
   }
   j.SupportsRanges=r.StatusCode==HttpStatusCode.PartialContent;
   j.D.Size=total;j.D.ETag=etag;j.D.LastModified=r.Headers["Last-Modified"];Save(j);Save(j);
  }
 }
 public void Recover(Job j) {
  j.D.State="Verificando partes guardadas";Directory.CreateDirectory(j.D.Work);var candidates=new Dictionary<string,Block>();
  foreach(string file in new[]{"ledger.a","ledger.b"}){string p=Path.Combine(j.D.Work,file);if(!File.Exists(p))continue;foreach(string line in File.ReadLines(p)){try{var b=Store.Unwrap<Block>(Encoding.UTF8.GetBytes(line));if(b.Offset<0||b.Length<=0||b.Length>4*1024*1024||b.Hash==null||b.Hash.Length!=64)continue;candidates[b.Offset+":"+b.Length+":"+b.Hash]=b;}catch{}}}
  var valid=new List<Block>();if(File.Exists(j.D.Part))using(var f=new FileStream(j.D.Part,FileMode.Open,FileAccess.Read,FileShare.Read)){long prev=0;foreach(var b in candidates.Values.OrderBy(b=>b.Offset)){
   Check(j);if(b.Offset<prev||b.Offset>f.Length-b.Length||(j.D.Size>=0&&b.Offset>j.D.Size-b.Length))continue;
   byte[] bytes=new byte[b.Length];f.Position=b.Offset;int done=0,n;while(done<bytes.Length&&(n=f.Read(bytes,done,bytes.Length-done))>0)done+=n;
   if(done==b.Length&&Store.Hash(bytes)==b.Hash){valid.Add(b);prev=b.Offset+b.Length;}
  }}
  j.Blocks=valid;j.D.Saved=valid.Sum(b=>(long)b.Length);
  // Rewrite clean journals individually; either old or new complete copy survives interruption.
  byte[] ledger=Encoding.UTF8.GetBytes(string.Join("\n",valid.Select(b=>Encoding.UTF8.GetString(Store.Wrap(b))))+(valid.Count>0?"\n":""));
  Store.Atomic(Path.Combine(j.D.Work,"ledger.a"),ledger);Store.Atomic(Path.Combine(j.D.Work,"ledger.b"),ledger);Save(j);
 }
 void Commit(Job j,FileStream output,long offset,MemoryStream pending){if(pending.Length==0)return;byte[] bytes=pending.ToArray();var b=new Block{Offset=offset,Length=bytes.Length,Hash=Store.Hash(bytes)};
  Interlocked.Increment(ref j.ActiveCommits);try{lock(j.Gate){output.Position=offset;output.Write(bytes,0,bytes.Length);output.Flush(true);byte[] record=Store.Wrap(b);Store.Append(Path.Combine(j.D.Work,"ledger.a"),record);Store.Append(Path.Combine(j.D.Work,"ledger.b"),record);j.Blocks.Add(b);j.D.Saved+=bytes.Length;j.D.LastCheckpoint=DateTime.UtcNow;}}finally{Interlocked.Decrement(ref j.ActiveCommits);}
  pending.SetLength(0);
 }
 int ReadResult(Task<int> task){try{return task.GetAwaiter().GetResult();}catch(IOException){throw new WebException("Lectura de red interrumpida",WebExceptionStatus.ConnectionClosed);}}
 bool ReadReady(Task<int> task){try{return task.Wait(100);}catch(AggregateException){ReadResult(task);throw;}}
 void DownloadRange(Job j,FileStream output,long start,long end,bool allowFull) {
  Check(j);var request=Request(j,start,end,true);
  using(var response=Response(request)){
   if(response.StatusCode==HttpStatusCode.PartialContent){if(ParseRange(response,start,end)!=j.D.Size)throw new NeedAction("El tamaño remoto cambió durante la descarga.");}
   else if(!(allowFull&&start==0&&end==j.D.Size-1&&response.StatusCode==HttpStatusCode.OK&&response.ContentLength==j.D.Size))throw new NeedAction("El servidor ignoró el rango solicitado. Se preservan las partes existentes.");
   string et=response.Headers["ETag"];if(!string.IsNullOrEmpty(j.D.ETag)&&!j.D.ETag.StartsWith("W/")&&et!=j.D.ETag)throw new NeedAction("El archivo cambió durante la descarga. Se detuvo para proteger el progreso.");
   using(var source=response.GetResponseStream())using(var pending=new MemoryStream()){
    byte[] buf=new byte[64*1024];long position=start,blockStart=start;var checkpoint=Stopwatch.StartNew();var rate=Stopwatch.StartNew();long rateBytes=0;
    try{while(position<=end){Check(j);int want=(int)Math.Min(Math.Min(buf.Length,4*1024*1024-pending.Length),end-position+1);var read=source.ReadAsync(buf,0,want);var readWatch=Stopwatch.StartNew();
      while(!ReadReady(read)){if(readWatch.ElapsedMilliseconds>60000){request.Abort();throw new WebException("Tiempo de espera agotado",WebExceptionStatus.Timeout);}if(j.Stop||Closing){request.Abort();throw new OperationCanceledException();}if(checkpoint.ElapsedMilliseconds>=5000&&pending.Length>0){Commit(j,output,blockStart,pending);blockStart=position;checkpoint.Restart();}}
      int n=ReadResult(read);if(n==0)throw new WebException("Transferencia truncada",WebExceptionStatus.ConnectionClosed);
      pending.Write(buf,0,n);position+=n;rateBytes+=n;Interlocked.Add(ref j.NetworkBytes,n);Interlocked.Exchange(ref j.LastReceiveTicks,DateTime.UtcNow.Ticks);
      if(pending.Length>=4*1024*1024||checkpoint.ElapsedMilliseconds>=5000){Commit(j,output,blockStart,pending);blockStart=position;checkpoint.Restart();}
      int limit=j.D.LimitKiB>0?j.D.LimitKiB:int.MaxValue;if(GlobalLimitKiB>0)limit=Math.Min(limit,GlobalLimitKiB);
      if(limit!=int.MaxValue){double target=(double)rateBytes/(limit*1024.0/Math.Max(1,j.D.Connections))*1000;while(rate.ElapsedMilliseconds<target){Check(j);if(checkpoint.ElapsedMilliseconds>=5000&&pending.Length>0){Commit(j,output,blockStart,pending);blockStart=position;checkpoint.Restart();}Thread.Sleep(50);}}
    }}finally{Commit(j,output,blockStart,pending);}
   }
  }
 }
 public void Run(Job j){
  if(j.D.Completed)return;
  Check(j);
  // Crash after rename but before completed metadata: verify published file, never overwrite it.
  if(!File.Exists(j.D.Part)&&File.Exists(j.D.Destination)&&!string.IsNullOrEmpty(j.D.ActualHash)){
   if(Store.HashFile(j.D.Destination)==j.D.ActualHash){j.D.Completed=true;j.D.Desired=false;j.D.State="Completado";Save(j);return;}throw new NeedAction("Ya existe un archivo distinto en el destino. No se sobrescribirá.");
  }
  Recover(j);Check(j);if(j.D.Size<0||j.D.Saved!=j.D.Size)Probe(j);Check(j);
  if(File.Exists(j.D.Destination))throw new NeedAction("Ya existe un archivo en el destino. No se sobrescribirá.");
  j.D.State="Descargando";j.D.Error="";
  var holes=new List<Tuple<long,long>>();long pos=0;foreach(var b in j.Blocks.OrderBy(b=>b.Offset)){if(pos<b.Offset)holes.Add(Tuple.Create(pos,b.Offset-1));pos=b.Offset+b.Length;}if(pos<j.D.Size)holes.Add(Tuple.Create(pos,j.D.Size-1));
  bool fresh=j.Blocks.Count==0;
  using(var output=new FileStream(j.D.Part,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read)){
   Store.EnableSparse(output);
   if(j.SupportsRanges&&j.D.Connections>1&&j.D.Size>1024*1024&&(!string.IsNullOrEmpty(j.D.ExpectedHash)||(!string.IsNullOrEmpty(j.D.ETag)&&!j.D.ETag.StartsWith("W/")))) {
    var ranges=new List<Tuple<long,long>>();foreach(var h in holes){long size=h.Item2-h.Item1+1;long part=8L*1024*1024;for(long a=h.Item1;a<=h.Item2;){long b=Math.Min(h.Item2,a+part-1);ranges.Add(Tuple.Create(a,b));a=b+1;}}
    Exception failure=null;object failureGate=new object();
    var queue=new ConcurrentQueue<Tuple<long,long>>(ranges);
    Parallel.For(0,Math.Min(j.D.Connections,ranges.Count),worker=>{Tuple<long,long> h;while(!j.Stop&&!Closing&&queue.TryDequeue(out h)){try{Check(j);DownloadRange(j,output,h.Item1,h.Item2,false);}catch(Exception ex){lock(failureGate){if(failure==null||failure is OperationCanceledException)failure=ex;}j.Stop=true;}}});
    if(failure!=null)throw failure;
   }else if(holes.Count==1&&fresh){DownloadRange(j,output,0,j.D.Size-1,true);}
   else {foreach(var h in holes){Check(j);DownloadRange(j,output,h.Item1,h.Item2,false);}}
   if(j.D.Size==0){output.SetLength(0);output.Flush(true);}
  }
  Check(j);j.D.State="Verificando SHA-256";j.D.ActualHash=Store.HashFile(j.D.Part);
  if(new FileInfo(j.D.Part).Length!=j.D.Size)throw new NeedAction("El tamaño final no coincide; se conservó el parcial.");
  if(!string.IsNullOrEmpty(j.D.ExpectedHash)&&!j.D.ExpectedHash.Equals(j.D.ActualHash,StringComparison.OrdinalIgnoreCase))throw new NeedAction("El SHA-256 no coincide. Archivo conservado; no se reinicia automáticamente.");
  j.D.State="Publicando";Save(j);File.Move(j.D.Part,j.D.Destination);j.D.Saved=j.D.Size;j.D.Completed=true;j.D.Desired=false;j.D.State="Completado";j.D.Error="";j.Attempts=0;Save(j);
 }
 public void Dispose(){Closing=true;lock(Gate)foreach(var j in Jobs)j.Stop=true;if(scheduler!=null)scheduler.Join(25000);lock(Gate)foreach(var j in Jobs)try{Save(j);}catch{} }
}
}
