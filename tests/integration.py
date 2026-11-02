import os, sys, time, json, hashlib, threading, subprocess, pathlib, http.server, re, base64, shutil
ROOT=pathlib.Path(__file__).resolve().parents[1]
EXE=ROOT/'DrexDownloads.exe'
WORK=ROOT/'tests'/('run-'+time.strftime('%Y%m%d-%H%M%S'))
WORK.mkdir()
DATA=(bytes(range(256))*65536) # 16 MiB; exercises two bounded parallel ranges
HASH=hashlib.sha256(DATA).hexdigest()
requests=[]
flaky_sent=False
busy_sent=False
class Handler(http.server.BaseHTTPRequestHandler):
 protocol_version='HTTP/1.1'
 def log_message(self,*a):pass
 def do_GET(self):
  path=self.path.split('?')[0]
  if path=='/large':
   total=18*1024**3;ran=self.headers.get('Range','');m=re.match(r'bytes=(\d+)-(\d+)',ran)
   if not m:self.send_response(400);self.send_header('Content-Length','0');self.end_headers();return
   a,b=map(int,m.groups());length=b-a+1
   if length>8*1024*1024:self.send_response(413);self.send_header('Content-Length','0');self.end_headers();return
   requests.append((path,a,b,length));self.send_response(206);self.send_header('Content-Range',f'bytes {a}-{b}/{total}');self.send_header('Content-Length',str(length));self.send_header('ETag','"huge-stable"');self.end_headers()
   try:
    chunk=bytes(range(256))*256
    left=length
    while left:n=min(left,len(chunk));self.wfile.write(chunk[:n]);self.wfile.flush();left-=n
   except (BrokenPipeError,ConnectionResetError,ConnectionAbortedError):pass
   return
  global busy_sent
  if path=='/busy' and not busy_sent:
   busy_sent=True;self.send_response(429);self.send_header('Retry-After','2');self.send_header('Content-Length','0');self.end_headers();return
  if path=='/denied':
   self.send_response(403);self.send_header('Content-Length','0');self.end_headers();return
  data=b'' if path=='/empty' else DATA
  et='"different"' if path=='/changed' else '"stable-object-v1"'
  ran=self.headers.get('Range');start,end=0,len(data)-1
  ranged=bool(ran and path!='/norange' and data)
  if ranged:
   match=re.match(r'bytes=(\d+)-(\d*)',ran);start=int(match[1]);end=int(match[2]) if match[2] else len(data)-1
   if start>=len(data):self.send_response(416);self.send_header('Content-Length','0');self.end_headers();return
   end=min(end,len(data)-1)
  global flaky_sent
  cut=path=='/flaky' and len(data)>0 and end-start>1 and not flaky_sent
  if cut: flaky_sent=True
  body=data[start:end+1] if data else b''
  requests.append((path,start,end,len(body)))
  self.send_response(206 if ranged else 200)
  self.send_header('Content-Length',str(len(body)))
  if path!='/noidentity': self.send_header('ETag',et)
  if ranged:self.send_header('Content-Range',f'bytes {start}-{end}/{len(data)}')
  self.send_header('Content-Type','text/html' if path=='/html-error' else 'application/octet-stream');self.end_headers()
  try:
   for i in range(0,len(body),65536):
    self.wfile.write(body[i:i+65536]);self.wfile.flush()
    if cut and i>=2*1024*1024:
     self.close_connection=True;return
  except (BrokenPipeError,ConnectionResetError,ConnectionAbortedError):pass
class QuietServer(http.server.ThreadingHTTPServer):
 def handle_error(self,*args): pass
server=QuietServer(('127.0.0.1',0),Handler)
threading.Thread(target=server.serve_forever,daemon=True).start()
BASE=f'http://127.0.0.1:{server.server_port}'
results=[]
def cmd(case,path='/file',hash_=HASH,connections=2,limit=0):
 d=WORK/case;d.mkdir(exist_ok=True);out=d/'out';out.mkdir(exist_ok=True)
 return [str(EXE),'--test-download',str(d/'state'),BASE+path,str(out),'prueba.bin',hash_,str(connections),str(limit)]
def run(case,path='/file',hash_=HASH,connections=2,limit=0):
 c=cmd(case,path,hash_,connections,limit);p=subprocess.run(c,timeout=100);return p.returncode,json.loads((WORK/case/'state'/'result.json').read_text(encoding='utf-8-sig'))
def result(name,check,detail=''):
 results.append({'name':name,'passed':bool(check),'detail':detail});print(('PASS ' if check else 'FAIL ')+name+' '+detail,flush=True)
def partial(case,path='/file',hash_=HASH,connections=1):
 c=cmd(case,path,hash_,connections,512);p=subprocess.Popen(c)
 ledger=None;deadline=time.time()+20
 while time.time()<deadline:
  parts=list((WORK/case/'out').glob('.drex-partials/*/ledger.a'))
  if parts and parts[0].stat().st_size>0:ledger=parts[0];break
  if p.poll() is not None:raise RuntimeError('process ended early '+str(p.returncode))
  time.sleep(.1)
 p.kill();p.wait(timeout=5)
 if not ledger:raise RuntimeError('no checkpoint in 20s')
 return ledger.parent

def main():
 work=partial('large_virtual','/large',hash_='',connections=2)
 part=work/'download.part'
 import ctypes
 attributes=ctypes.windll.kernel32.GetFileAttributesW(str(part))
 result('large_file_sparse_flag',bool(attributes&0x200))
 result('large_file_bounded_frontier',part.stat().st_size<32*1024*1024,f'logical_bytes={part.stat().st_size}, remote_bytes={18*1024**3}')
 result('large_file_bounded_requests',all(z[3]<=8*1024*1024 for z in requests if z[0]=='/large'))
 rc,d=run('html_error','/html-error');result('html_error_not_saved_as_binary',rc==2 and not d['Completed'])
 rc,d=run('normal');result('normal_parallel_hash',rc==0 and d['Completed'] and d['ActualHash']==HASH)
 rc,d=run('no_range','/norange');result('server_without_ranges_initial',rc==0 and d['ActualHash']==HASH)
 rc,d=run('zero','/empty',hashlib.sha256(b'').hexdigest());result('zero_byte_file',rc==0 and d['Completed'] and d['Size']==0)
 rc,d=run('bad_hash',hash_='0'*64);result('wrong_hash_preserves_partial',rc==2 and not d['Completed'] and pathlib.Path(d['Folder']).joinpath('.drex-partials',d['Id'],'download.part').exists())
 result('wrong_hash_no_final_file',not (WORK/'bad_hash'/'out'/'prueba.bin').exists())
 work=partial('crash');size=(work/'download.part').stat().st_size;rc,d=run('crash');result('killed_process_resume',rc==0 and d['ActualHash']==HASH, f'checkpoint bytes={size}')
 work=partial('corrupt_block');p=work/'download.part'
 with p.open('r+b') as f:f.seek(100);f.write(b'CORRUPTED')
 (work/'ledger.a').write_bytes((work/'ledger.a').read_bytes()+b'{torn')
 rc,d=run('corrupt_block');result('corrupt_block_and_torn_journal_recovery',rc==0 and d['ActualHash']==HASH)
 work=partial('metadata',hash_='');metas=list((WORK/'metadata'/'state'/'jobs').glob('*.json'));latest=max(metas,key=lambda p:p.stat().st_mtime_ns);latest.write_bytes(b'{bad')
 rc,d=run('metadata',hash_='');result('metadata_fallback',rc==0 and d['ActualHash']==HASH)
 work=partial('changed',hash_='');before=hashlib.sha256((work/'download.part').read_bytes()).hexdigest();rc,d=run('changed','/changed',hash_='');after=hashlib.sha256((work/'download.part').read_bytes()).hexdigest();result('changed_identity_preserved',rc==2 and before==after and not d['Completed'])
 work=partial('expired');before=hashlib.sha256((work/'download.part').read_bytes()).hexdigest();rc,d=run('expired','/denied');result('expired_link_preserved',rc==2 and before==hashlib.sha256((work/'download.part').read_bytes()).hexdigest());rc,d=run('expired','/file?new=1');result('renewed_link_resume',rc==0 and d['ActualHash']==HASH)
 work=partial('range_disappears');before=hashlib.sha256((work/'download.part').read_bytes()).hexdigest();rc,d=run('range_disappears','/norange');result('range_disappears_no_overwrite',rc==2 and before==hashlib.sha256((work/'download.part').read_bytes()).hexdigest())
 work=partial('missing_identity','/noidentity',hash_='');rc,d=run('missing_identity','/noidentity',hash_='');result('missing_identity_blocks_resume',rc==2 and not d['Completed'])
 # Repair exactly one corrupt block while keeping another known good block.
 state=WORK/'bad_hash'/'state'
 for p in (state/'jobs').glob('*.json'):
  envelope=json.loads(p.read_text(encoding='utf-8-sig'));obj=json.loads(base64.b64decode(envelope['Payload']));obj['ExpectedHash']=HASH
  payload=json.dumps(obj,separators=(',',':')).encode();p.write_text(json.dumps({'Payload':base64.b64encode(payload).decode(),'Hash':hashlib.sha256(payload).hexdigest()}),encoding='utf-8')
 part=next((WORK/'bad_hash'/'out').glob('.drex-partials/*/download.part'))
 with part.open('r+b') as f:f.seek(64);f.write(b'DAMAGED')
 req_before=len(requests);rc,d=run('bad_hash');new_requests=requests[req_before:];transferred=sum(x[3] for x in new_requests)
 result('selective_repair_keeps_healthy_block',rc==0 and d['ActualHash']==HASH and transferred<len(DATA),f'rerequested={transferred} of {len(DATA)}')
 check=subprocess.run([str(ROOT/'tests'/'EngineChecks.exe'),str(WORK/'enginechecks'),BASE,HASH],capture_output=True,text=True,timeout=100)
 for line in check.stdout.splitlines():
  if line.startswith(('PASS ','FAIL ')):result(line[5:],line.startswith('PASS '))
 result('engine_lifecycle_suite_exit',check.returncode==0,check.stderr[:200])
 # Crash at publication: completed file exists, metadata still in publishing state.
 state=WORK/'normal'/'state';jobs=list((state/'jobs').glob('*.json'))
 for p in jobs:
  envelope=json.loads(p.read_text(encoding='utf-8-sig'));obj=json.loads(base64.b64decode(envelope['Payload']));obj['Completed']=False;obj['Desired']=True;obj['State']='Publicando'
  payload=json.dumps(obj,separators=(',',':')).encode();envelope={'Payload':base64.b64encode(payload).decode(),'Hash':hashlib.sha256(payload).hexdigest()};p.write_text(json.dumps(envelope),encoding='utf-8')
 rc,d=run('normal');result('publication_crash_reconciled',rc==0 and d['Completed'] and d['ActualHash']==HASH)
try:main()
except Exception as ex:result('harness_exception',False,repr(ex))
finally:
 report={'results':results,'passed':sum(x['passed'] for x in results),'total':len(results),'workdir':str(WORK)}
 (WORK/'report.json').write_text(json.dumps(report,indent=2));(ROOT/'tests'/'latest-report.json').write_text(json.dumps(report,indent=2));server.shutdown()
 sys.exit(0 if results and all(x['passed'] for x in results) else 1)
