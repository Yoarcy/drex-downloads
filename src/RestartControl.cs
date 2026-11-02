using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Microsoft.Win32;
namespace DrexDownloads {
[DataContract] public class RestartState {
 [DataMember] public DateTime BootUtc;
 [DataMember] public string[] Jobs;
}
public static class RestartControl {
 [DllImport("kernel32.dll")] static extern ulong GetTickCount64();
 const string Value="DrexDownloadsRecovery";
 public static DateTime BootUtc {get{return DateTime.UtcNow.AddMilliseconds(-(double)GetTickCount64());}}
 static string Marker(string root){return Path.Combine(root,"data","restart.json");}
 public static void Update(string root,Engine engine,string exe,bool register=true){
  string[] jobs;bool active;lock(engine.Gate){jobs=engine.Jobs.Where(j=>j.D.Desired&&!j.D.Completed&&!j.D.Removed).Select(j=>j.D.Id).ToArray();active=engine.Jobs.Any(j=>j.D.Desired&&!j.D.Completed&&!j.D.Removed&&(j.Working||j.D.State=="Esperando red"));}
  if(!active||jobs.Length==0){Disarm(root,register);return;}
  var state=new RestartState{BootUtc=BootUtc,Jobs=jobs};
  bool write=true;try{var old=Store.Unwrap<RestartState>(File.ReadAllBytes(Marker(root)));write=Math.Abs((old.BootUtc-state.BootUtc).TotalSeconds)>30||!old.Jobs.SequenceEqual(jobs);}catch{}
  if(write)Store.Atomic(Marker(root),Store.Wrap(state));
  if(register)using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){string command="\""+exe+"\" --recover-after-restart";if((string)key.GetValue(Value)!=command)key.SetValue(Value,command);}
 }
 public static void Disarm(string root,bool register=true){if(File.Exists(Marker(root)))File.Delete(Marker(root));if(register)using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)){if(key!=null)key.DeleteValue(Value,false);}}
 public static bool ShouldRecover(string root,DateTime boot){
  try{
   var state=Store.Unwrap<RestartState>(File.ReadAllBytes(Marker(root)));
   if(Math.Abs((boot-state.BootUtc).TotalSeconds)<30)return false;
   foreach(var id in state.Jobs??new string[0]){
    Guid parsed;if(!Guid.TryParseExact(id,"N",out parsed))continue;
    JobData latest=null;foreach(int slot in new[]{0,1})try{var d=Store.Unwrap<JobData>(File.ReadAllBytes(Path.Combine(root,"data","jobs",id+"."+slot+".json")));if(latest==null||d.Generation>latest.Generation)latest=d;}catch{}
    if(latest!=null&&latest.Desired&&!latest.Completed&&!latest.Removed)return true;
   }
  }catch{}
  return false;
 }
}
}
