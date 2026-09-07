"""Build an honest edited gameplay demo from local capture MP4/WAV pairs.

Python3.10+, Pillow, and FFmpeg are required. No footage is synthesized.
Usage: python build_gameplay.py --ffmpeg path/to/ffmpeg.exe --plan edit-plan.json
Raw recordings remain local and are not bundled with the public build script.
"""
from __future__ import annotations
import argparse,hashlib,json,os,subprocess,inspect
from pathlib import Path
from datetime import datetime
from PIL import Image,ImageDraw,ImageFont

HERE=Path(__file__).resolve().parent
REPO=next(p for p in HERE.parents if (p/'thunderstore').is_dir())
W,H,FPS=1920,1080,30
BG=(12,20,29,228);INK='#f4f7fa';MUTED='#c0cbd5';CYAN='#76ecd0';GOLD='#ffe678'

def font(size,bold=False):
 path=Path(os.environ.get('WINDIR','C:/Windows'))/'Fonts'/('segoeuib.ttf' if bold else 'segoeui.ttf')
 if not path.exists():path=Path('/usr/share/fonts/truetype/dejavu')/('DejaVuSans-Bold.ttf' if bold else 'DejaVuSans.ttf')
 return ImageFont.truetype(str(path),size)

def overlay(path,part):
 im=Image.new('RGBA',(W,H),(0,0,0,0));d=ImageDraw.Draw(im)
 special=part.get('style')
 if special == 'intro':
  d.rounded_rectangle((56,831,756,980),radius=16,fill=BG)
  d.rounded_rectangle((56,843,62,968),radius=3,fill=CYAN)
  d.text((84,844),part['tag'],font=font(20,True),fill=CYAN)
  d.text((84,878),part['title'],font=font(38,True),fill=INK)
  d.text((84,932),part['detail'],font=font(25),fill=MUTED)
  mascot=Image.open(REPO/'docs/promotion/logo-options/option-b-console-companion.png').convert('RGBA').resize((170,170),Image.Resampling.LANCZOS)
  mask=Image.new('L',(170,170));ImageDraw.Draw(mask).rounded_rectangle((0,0,169,169),radius=22,fill=255)
  im.paste(mascot,(1668,801),mask)
 elif special == 'outro':
  d.rounded_rectangle((56,779,1245,980),radius=18,fill=BG)
  d.rounded_rectangle((56,793,62,966),radius=3,fill=CYAN)
  d.text((86,796),part['tag'],font=font(23,True),fill=CYAN)
  d.text((86,829),part['title'],font=font(46,True),fill=INK)
  d.text((86,891),part['detail'],font=font(27),fill=MUTED)
  if part.get('url'):d.text((86,932),part['url'],font=font(23),fill=GOLD)
  mascot=Image.open(REPO/'docs/promotion/logo-options/option-b-console-companion.png').convert('RGBA')
  mascot=mascot.resize((170,170),Image.Resampling.LANCZOS)
  mask=Image.new('L',(170,170));ImageDraw.Draw(mask).rounded_rectangle((0,0,169,169),radius=22,fill=255)
  im.paste(mascot,(1668,801),mask)
 else:
  d.rounded_rectangle((56,831,756,980),radius=16,fill=BG)
  d.rounded_rectangle((56,843,62,968),radius=3,fill=CYAN)
  d.text((84,844),part['tag'],font=font(20,True),fill=CYAN)
  d.text((84,878),part['title'],font=font(33,True),fill=INK)
  d.text((84,929),part['detail'],font=font(23),fill=MUTED)
 im.save(path)

def seconds(value):return datetime.fromisoformat(value.replace('Z','+00:00')).timestamp()
def offset(sources,source):
 audio=[json.loads(s) for s in (sources/(source+'.audio.log')).read_text().splitlines() if s.startswith('{')][-1]
 video=json.loads((sources/(source+'.video-start.json')).read_text(encoding='utf-8-sig'))
 return round(seconds(video['StartedUtc'])-seconds(audio['StartedUtc']),6)

def stamp(t):
 ms=round(t*1000);h,ms=divmod(ms,3600000);m,ms=divmod(ms,60000);s,ms=divmod(ms,1000)
 return f'{h:02}:{m:02}:{s:02},{ms:03}'
def chapterstamp(t):
 whole=int(t);m,s=divmod(whole,60);return f'{m}:{s:02}'

def run(cmd,log):
 flags=getattr(subprocess,'CREATE_NO_WINDOW',0)|getattr(subprocess,'BELOW_NORMAL_PRIORITY_CLASS',0)
 with log.open('w') as output:
  subprocess.run(cmd,stdout=output,stderr=subprocess.STDOUT,check=True,creationflags=flags)

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--ffmpeg',required=True);ap.add_argument('--plan',type=Path,default=HERE/'edit-plan.json');ap.add_argument('--sources',type=Path,default=REPO/'.local/recordings');ap.add_argument('--work',type=Path,default=REPO/'.local/recordings/final-edit');ap.add_argument('--out',type=Path);ap.add_argument('--overlays-only',action='store_true');args=ap.parse_args()
 work=args.work;work.mkdir(parents=True,exist_ok=True);cache=work/'segments';cache.mkdir(exist_ok=True)
 out=args.out or work/'gameplay-assembly-1080p.mp4';out.parent.mkdir(parents=True,exist_ok=True)
 plan=json.loads(args.plan.read_text(encoding='utf-8-sig'));parts=plan['segments'];cursor=0.;timeline=[];rendered=[]
 for number,part in enumerate(parts,1):
  duration=round((part['out']-part['in'])*FPS)/FPS
  audio_offset=offset(args.sources,part['source'])
  entry=dict(part);entry.update(start=round(cursor,3),end=round(cursor+duration,3),duration=round(duration,3),audio_offset_estimate=audio_offset);timeline.append(entry);cursor+=duration
  fingerprint=hashlib.sha256(json.dumps({'part':part,'offset':audio_offset,'overlay':hashlib.sha256(inspect.getsource(overlay).encode()).hexdigest(),'encoding_revision':1},sort_keys=True).encode()).hexdigest()[:12]
  stem=f'{number:02}-{fingerprint}';png=cache/(stem+'.png');mp4=cache/(stem+'.mp4');overlay(png,part);rendered.append(mp4)
  if args.overlays_only:continue
  if mp4.exists():print(f'Cached {number:02}/{len(parts)}',flush=True);continue
  print(f'Encoding {number:02}/{len(parts)}: {part["source"]} {part["in"]:.1f}-{part["out"]:.1f}s',flush=True)
  # Captions stay below the console and away from health, inventory and quota HUD.
  # SFX retain their original timing and dynamics. Short fades only prevent edit clicks.
  graph=f'[0:v]fps={FPS},setsar=1[v0];[v0][2:v]overlay=0:0:format=auto,format=yuv420p[v];[1:a]asetpts=PTS-STARTPTS,volume=0.84,afade=t=in:d=0.025,afade=t=out:st={max(0,duration-.025):.6f}:d=0.025,alimiter=limit=0.9:level=false[a]'
  cmd=[args.ffmpeg,'-y','-hide_banner','-loglevel','warning','-threads','2','-ss',str(part['in']),'-i',str(args.sources/(part['source']+'.mp4')),'-ss',str(part['in']+audio_offset),'-i',str(args.sources/(part['source']+'.wav')),'-loop','1','-i',str(png),'-filter_complex_threads','1','-filter_complex',graph,'-map','[v]','-map','[a]','-t',str(duration),'-c:v','libx264','-preset','fast','-crf','22','-maxrate','3500k','-bufsize','7000k','-threads','2','-r',str(FPS),'-c:a','aac','-b:a','160k','-ar','48000','-movflags','+faststart',str(mp4)]
  run(cmd,cache/(stem+'.log'))
 if args.overlays_only:return
 concat=work/'concat.txt';concat.write_text(''.join("file '"+str(p.resolve()).replace('\\','/').replace("'","'\\''")+"'\n" for p in rendered))
 run([args.ffmpeg,'-y','-hide_banner','-loglevel','warning','-f','concat','-safe','0','-i',str(concat),'-c:v','copy','-c:a','aac','-b:a','160k','-af','aresample=async=1:first_pts=0','-t',str(round(cursor,3)),'-movflags','+faststart',str(out)],work/'concat.log')
 # Merge repeated overlays across adjacent edits into readable caption events.
 events=[]
 for item in timeline:
  caption=item['tag']+' — '+item['title']+'\n'+item['detail']
  if item.get('url'):caption+='\n'+item['url']
  if events and events[-1]['text']==caption:events[-1]['end']=item['end']
  else:events.append({'start':item['start'],'end':item['end'],'text':caption})
 out.with_suffix('.srt').write_text('\n\n'.join(f'{i}\n{stamp(e["start"])} --> {stamp(e["end"])}\n{e["text"]}' for i,e in enumerate(events,1))+'\n',encoding='utf-8')
 phases=[]
 for item in timeline:
  if item['phase']=='Outro':continue
  if not phases or phases[-1]['title']!=item['phase']:phases.append({'start':item['start'],'title':item['phase']})
 chapters=[]
 for i,phase in enumerate(phases):
  nextstart=phases[i+1]['start'] if i+1<len(phases) else cursor
  if nextstart-phase['start']>=10:chapters.append(phase)
 if chapters:chapters[0]['start']=0
 chaptertext='\n'.join(chapterstamp(p['start'])+' '+p['title'] for p in chapters)+'\n'
 out.with_suffix('.chapters.txt').write_text(chaptertext,encoding='utf-8')
 metadata={'title':'Repo Command Console — Spawn, grab, and clean up in R.E.P.O.','version':'2.2.0','duration_seconds':round(cursor,3),'width':W,'height':H,'fps':FPS,'audio':'Original game/output SFX; no narration or added music','provenance':'Edited genuine gameplay at native speed; idle waits removed; approved mascot only in small intro/outro overlays. No generated gameplay.','audio_alignment':'Per-take process-clock sidecar offset estimates; not hardware sample timestamps.','enemy_sequence_pending':plan.get('enemy_sequence_pending',False),'chapters':chapters,'timeline':timeline,'output':out.name,'bytes':out.stat().st_size,'sha256':hashlib.sha256(out.read_bytes()).hexdigest()}
 out.with_suffix('.metadata.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
 print(json.dumps({'output':str(out),'duration':round(cursor,3),'bytes':out.stat().st_size,'pending_enemy':metadata['enemy_sequence_pending']}),flush=True)
if __name__=='__main__':main()


