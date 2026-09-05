"""Inspect the built Quest package and its ARM64 native library without a headset."""
import zipfile,hashlib,json,subprocess,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];apk=ROOT/'builds/AssetDemo/Android/ThoraxLab.apk';android=Path('C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Data/PlaybackEngines/AndroidPlayer');out=ROOT/'qa/reports';checks=[]
def check(name,value,detail=''):checks.append({'name':name,'passed':bool(value),'detail':detail})
with zipfile.ZipFile(apk) as z:
 check('ZIP integrity',z.testzip() is None);names=z.namelist();libs=[p for p in names if p.startswith('lib/') and p.endswith('.so')];check('ARM64 libraries only',bool(libs) and all('/arm64-v8a/' in p for p in libs),libs)
 name='lib/arm64-v8a/libthoraxcut.so';check('native cutting library packaged',name in names);data=z.read(name);check('ELF64 AArch64',data[:4]==b'\x7fELF' and data[4]==2 and struct.unpack_from('<H',data,18)[0]==183)
 check('attribution packaged',any(p.endswith('/ATTRIBUTION.txt') for p in names));check('complete MAL terms packaged',any(p.endswith('/MAL-Atlas-License.txt') for p in names));check('Apache license packaged',any(p.endswith('/Manifold-Apache-2.0.txt') for p in names))
 extracted=ROOT/'.tmp/apk-thoraxcut.so';extracted.write_bytes(data)
 phoff=struct.unpack_from('<Q',data,32)[0];size,count=struct.unpack_from('<HH',data,54);alignments=[struct.unpack_from('<Q',data,phoff+i*size+48)[0] for i in range(count) if struct.unpack_from('<I',data,phoff+i*size)[0]==1];check('16 KB ELF load alignment',alignments and all(a>=16384 for a in alignments),alignments)
 badging=subprocess.run([str(android/'SDK/build-tools/36.0.0/aapt.exe'),'dump','badging',str(apk)],capture_output=True,text=True);(out/'apk-badging.txt').write_text(badging.stdout);check('package identity',badging.returncode==0 and "name='org.thoraxlab.education'" in badging.stdout)
 signer=subprocess.run([str(android/'OpenJDK/bin/java.exe'),'-jar',str(android/'SDK/build-tools/36.0.0/lib/apksigner.jar'),'verify','--verbose',str(apk)],capture_output=True,text=True);(out/'apk-signature.txt').write_text(signer.stdout+signer.stderr);check('APK signature verifies',signer.returncode==0)
report={'apk':apk.relative_to(ROOT).as_posix(),'bytes':apk.stat().st_size,'sha256':hashlib.file_digest(apk.open('rb'),'sha256').hexdigest(),'checks':checks,'failures':sum(not c['passed'] for c in checks),'headset_run':False,'signing':'development/debug signing for local installation, not a store release'}
(out/'apk-verification.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2));assert report['failures']==0
