using System;using System.IO;using System.Threading;using System.Diagnostics;using DrexDownloads;
class EngineChecks{
 static void Assert(bool c,string n){Console.WriteLine((c?"PASS ":"FAIL ")+n);if(!c)throw new Exception(n);}
 static void Wait(Func<bool> f,int seconds){var s=Stopwatch.StartNew();while(!f()&&s.Elapsed.TotalSeconds<seconds)Thread.Sleep(100);if(!f())throw new Exception("Timeout");}
 static int Main(string[] args){try{
  string root=args[0],url=args[1],hash=args[2];Directory.CreateDirectory(root);string outdir=Path.Combine(root,"out");string id;long saved;
  using(var e=new Engine(Path.Combine(root,"queue"))){
   var j=e.Add(url+"/file",outdir,"pausa ñ con espacios.bin",hash,1,512,"",false);id=j.D.Id;
   e.Start();Thread.Sleep(600);Assert(!j.Working&&j.D.Saved==0,"paused_task_does_not_autostart");
   e.Resume(j);Wait(()=>j.D.Saved>0,15);e.Pause(j);Wait(()=>!j.Working,10);saved=j.D.Saved;Assert(!j.D.Desired&&saved>0&&!j.D.Completed,"pause_flushes_and_stops");
  }
  using(var e=new Engine(Path.Combine(root,"queue"))){var j=e.Jobs[0];Assert(!j.D.Desired,"manual_pause_survives_restart");j.D.LimitKiB=0;e.Start();Thread.Sleep(500);Assert(!j.Working,"restored_pause_is_respected");e.Resume(j);Wait(()=>j.D.Completed||!j.D.Desired,30);Assert(j.D.Completed&&j.D.ActualHash==hash,"resume_button_completes_exact_file");
   var one=e.Add(url+"/file?1",outdir,"one.bin",hash,2,0,"",false);var two=e.Add(url+"/file?2",outdir,"two.bin",hash,2,0,"",false);e.Move(two,-1);Assert(e.Jobs.IndexOf(two)<e.Jobs.IndexOf(one),"queue_reorder");e.Resume(one);e.Resume(two);Wait(()=>one.D.Completed&&two.D.Completed,30);Assert(one.D.ActualHash==hash&&two.D.ActualHash==hash,"queue_multiple_downloads");
   e.Remove(one);Assert(File.Exists(one.D.Destination),"remove_preserves_final_file");
   var collision=e.Add(url+"/file?collision",outdir,"two.bin",hash,1,0,"",false);Assert(collision.D.Name!="two.bin","filename_collision_preserves_original");
  }
  using(var e=new Engine(Path.Combine(root,"retry"))){var j=e.Add(url+"/flaky",outdir,"flaky.bin",hash,1,0,"",true);e.Start();Wait(()=>j.D.Completed||!j.D.Desired,45);Assert(j.D.Completed&&j.D.ActualHash==hash,"network_truncation_auto_recovers");}
  using(var e=new Engine(Path.Combine(root,"busy"))){var j=e.Add(url+"/busy",outdir,"busy.bin",hash,1,0,"",true);var watch=Stopwatch.StartNew();e.Start();Wait(()=>j.D.Completed||!j.D.Desired,20);Assert(j.D.Completed&&watch.Elapsed.TotalSeconds>=2,"retry_after_respected");}
  using(var e=new Engine(Path.Combine(root,"fourconnections"))){
   var j=e.Add(url+"/large",outdir,"four-connections.bin","",4,1024,"",true);e.Start();
   Wait(()=>j.ActiveConnections==4,20);Assert(j.ActiveConnections==4,"four_simultaneous_transfer_streams");
   e.Pause(j);Wait(()=>!j.Working,15);Assert(j.ActiveConnections==0&&j.D.Saved>0,"connection_counter_zero_after_pause");
  }
  string restartRoot=Path.Combine(root,"restartapp");
  using(var e=new Engine(Path.Combine(restartRoot,"data"))){
   var j=e.Add(url+"/file",outdir,"restart.bin",hash,1,0,"",false);
   RestartControl.Update(restartRoot,e,"dummy.exe",false);Assert(!File.Exists(Path.Combine(restartRoot,"data","restart.json")),"idle_no_autostart_marker");
   j.D.Desired=true;j.Working=true;e.Save(j);RestartControl.Update(restartRoot,e,"dummy.exe",false);
   Assert(!RestartControl.ShouldRecover(restartRoot,RestartControl.BootUtc),"same_boot_does_not_autolaunch");
   var marker=Path.Combine(restartRoot,"data","restart.json");var state=Store.Unwrap<RestartState>(File.ReadAllBytes(marker));state.BootUtc=RestartControl.BootUtc.AddDays(-1);Store.Atomic(marker,Store.Wrap(state));
   Assert(RestartControl.ShouldRecover(restartRoot,RestartControl.BootUtc),"interrupted_active_job_recovers_on_new_boot");
   j.D.Desired=false;e.Save(j);Assert(!RestartControl.ShouldRecover(restartRoot,RestartControl.BootUtc),"paused_metadata_overrides_stale_marker");
   j.D.Desired=true;j.D.Completed=true;e.Save(j);Assert(!RestartControl.ShouldRecover(restartRoot,RestartControl.BootUtc),"completed_job_does_not_autolaunch");
   j.D.Completed=false;e.Save(j);RestartControl.Disarm(restartRoot,false);Assert(!RestartControl.ShouldRecover(restartRoot,RestartControl.BootUtc),"clean_exit_disarms_recovery");
   File.WriteAllText(marker,"corrupted");Assert(!RestartControl.ShouldRecover(restartRoot,RestartControl.BootUtc),"corrupt_marker_does_not_autolaunch");
   j.Working=false;
  }
  using(var e=new Engine(Path.Combine(root,"autorefresh"))){
   var j=e.Add(url+"/file",outdir,"auto-refresh.bin",hash,1,512,"",true);e.Start();Wait(()=>j.D.Saved>0,15);e.Pause(j);Wait(()=>!j.Working,10);long kept=j.D.Saved;j.D.LimitKiB=0;e.Renew(j,url+"/refresh");Wait(()=>j.D.Completed||!j.D.Desired,35);Assert(j.D.Completed&&j.D.ActualHash==hash&&kept>0&&j.RefreshAttempts>0,"expired_link_auto_refresh_keeps_partial");
  }
  using(var e=new Engine(Path.Combine(root,"permanentdenial"))){var j=e.Add(url+"/denied",outdir,"denied.bin",hash,1,0,"",true);e.Start();Wait(()=>!j.D.Desired,15);Assert(!j.D.Completed&&j.RefreshAttempts==4,"permanent_auth_denial_stops_bounded");}
  using(var e=new Engine(Path.Combine(root,"offlineboot"))){var j=e.Add(url+"/offlineboot",outdir,"offline-boot.bin",hash,1,0,"",true);e.Start();Wait(()=>j.D.Completed||!j.D.Desired,100);Assert(j.D.Completed&&j.D.ActualHash==hash,"network_wait_survives_five_failures");}
  return 0;
 }catch(Exception ex){Console.WriteLine("FAIL "+ex.Message);return 1;}}
}
