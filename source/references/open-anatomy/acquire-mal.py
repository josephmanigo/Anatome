"""Recover original MAL meshes through the publisher's live atlas structure."""
import concurrent.futures
import hashlib
import json
from pathlib import Path
import subprocess
import time
from urllib.parse import quote

ROOT=Path(__file__).resolve().parent
BASE='https://www.openanatomy.org/atlases/macbioidi/mauritania/thorax-2020-11-15/'
atlas=json.loads((ROOT/'atlasStructure.json').read_text())
targets={'superior lobe of right lung','middle lobe of right lung','inferior lobe of right lung',
 'superior lobe of left lung','inferior lobe of left lung','heart','ribs','vertebra',
 'manubrium of sternum','body of sternum','xiphoid process','aorta','trachea',
 'carina of trachea','main bronchi','lobar bronchi','pulmonary arteries','pulmonary veins','esophagus'}
items=[x for x in atlas if x.get('@type')=='DataSource' and
       (Path(x.get('source','')).stem in targets or x.get('source','').endswith('.seg.nrrd'))]

def acquire(item):
 relative=item['source'];path=ROOT/relative;url=BASE+quote(relative)
 result={'atlas_source_id':item['@id'],'path':str(path.relative_to(ROOT)).replace('\\','/'),'url':url,
  'source_name':Path(relative).stem,'license':'3D Slicer License Section B',
  'acquired_utc':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),'coordinates_modified':False}
 try:
  path.parent.mkdir(exist_ok=True,parents=True)
  if not path.exists():
   tmp=path.with_suffix(path.suffix+'.partial')
   subprocess.run(['curl.exe','--fail','--location','--silent','--show-error','--retry','2','--output',str(tmp),url],check=True)
   tmp.replace(path)
  result.update(status='acquired',bytes=path.stat().st_size,sha256=hashlib.sha256(path.read_bytes()).hexdigest())
  print('ACQUIRED',relative,result['bytes'],flush=True)
 except Exception as e:result.update(status='failed',error=str(e));print('FAILED',relative,e,flush=True)
 return result

with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:records=list(pool.map(acquire,items))
manifest={'schema_version':1,'dataset':'Mauritanian Anatomy Laboratory Thoracic Atlas, January 2021',
 'publisher_page':'https://www.openanatomy.org/atlas-pages/atlas-mauritania-thorax.html',
 'atlas_structure_url':BASE+'atlasStructure.json','license_url':'https://www.openanatomy.org/atlas-pages/slicer-license.html#PART-B',
 'notice':'All or portions of this licensed product (such portions are the “Software”) have been obtained under license from The Brigham and Women’s Hospital, Inc. and are subject to the following terms and conditions:',
 'required_license_copy':'LICENSE-AND-NOTICE.txt (publisher Section B plus Section C retained)',
 'authors':['Haythem Guermazi','Ahmedou Moulaye IDRISS','Tfeil Yahya'],
 'source_specimen':'38-year-old male; independent CT-derived specimen, not BodyParts3D',
 'registration_status':'Original coordinates retained; no registration to BodyParts3D performed',
 'sources':records}
manifest['metadata_files']=[{'path':name,'sha256':hashlib.sha256((ROOT/name).read_bytes()).hexdigest()}
    for name in ('atlasStructure.json','viewer.html','configAtlasViewer-default.js','slicer-license.html','mal-thoracic-atlas.html')]
if (ROOT/'LICENSE-AND-NOTICE.txt').exists():
    manifest['notice_sha256']=hashlib.sha256((ROOT/'LICENSE-AND-NOTICE.txt').read_bytes()).hexdigest()
(ROOT/'mal-source-manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
if any(r['status']!='acquired' for r in records):raise SystemExit(1)
