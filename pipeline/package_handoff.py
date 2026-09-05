"""Package only the approved asset demo; preserve concurrent project work separately."""
import json,hashlib,zipfile,datetime
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];out=ROOT/'handoff';out.mkdir(exist_ok=True)
skip={'Library','Temp','Obj','Logs','UserSettings','__pycache__','.git','.tmp','.utmp','.gradle','.vs'}
def files(folder):
 return sorted(p for p in folder.rglob('*') if p.is_file() and not any(s in skip for s in p.relative_to(folder).parts) and p.suffix not in ('.blend1','.blend2') and not ('pipeline' in p.parts and ('learning' in p.name.lower() or p.name == 'import-tmp.ps1')))
def archive(name,paths):
 target=out/name
 with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=4,allowZip64=True) as z:
  for p in sorted(set(paths)):z.write(p,p.relative_to(ROOT).as_posix())
 with zipfile.ZipFile(target) as z:
  bad=z.testzip()
  if bad:raise ValueError('Archive CRC failed '+bad)
 return target
assets=archive('ThoraxLab-Assets-and-Sources.zip',sum([files(ROOT/p) for p in ['blender','exports','source','docs','pipeline','qa/reports','qa/renders']],[])+[ROOT/'README.md',ROOT/'ATTRIBUTION.txt'])
project=archive('ThoraxLab-Unity-AssetDemo.zip',files(ROOT/'unity/ThoraxAssetDemo')+[ROOT/'README.md',ROOT/'ATTRIBUTION.txt']+files(ROOT/'docs')+[p for p in (ROOT/'native').iterdir() if p.is_file() and p.suffix in ('.cpp','.h','.md','.txt')])
windows=archive('ThoraxLab-Windows.zip',[p for p in files(ROOT/'builds/AssetDemo/Windows') if 'DoNotShip' not in p.as_posix()]+[ROOT/'ATTRIBUTION.txt']+files(ROOT/'docs/licenses'))
artifacts=[assets,project,windows,ROOT/'builds/AssetDemo/Android/ThoraxLab.apk',ROOT/'blender/Thorax_MASTER.blend',ROOT/'blender/Laboratory_MASTER.blend',ROOT/'exports/Thorax.fbx',ROOT/'exports/Laboratory.fbx']
manifest={'created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'artifacts':[],'headset_measured':False,'professional_review_required':False}
for p in artifacts:
 if not p.is_file():raise FileNotFoundError(p)
 manifest['artifacts'].append({'path':p.relative_to(ROOT).as_posix(),'bytes':p.stat().st_size,'sha256':hashlib.file_digest(p.open('rb'),'sha256').hexdigest()})
(out/'SHA256.json').write_text(json.dumps(manifest,indent=2));print(json.dumps(manifest,indent=2))
