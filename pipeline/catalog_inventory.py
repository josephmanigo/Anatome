"""Generate a complete required-group mapping and handoff CSV from actual records."""
import json,csv,collections
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];catalog=json.loads((ROOT/'exports/anatomy-catalog.json').read_text())['records']
recon={r['id']:r for r in json.loads((ROOT/'docs/reconstructions.json').read_text())}
for r in catalog:
 if not r.get('required_group'):
  name=r['name'].lower()
  r['required_group']=next((group for token,group in [('lobe','lung_lobes'),('pleura','pleural_layers'),('pericard','pericardium'),('phrenic','phrenic_nerves'),('vagus','vagus_nerves'),('sympathetic','sympathetic_trunks'),('intercostal','intercostal_neurovascular_bundles'),('duct','thoracic_duct'),('fascia','thoracic_fascia'),('fat','thoracic_fat'),('subcutaneous','thoracic_fat')] if token in name),'UNMAPPED')
 if r['required_group']=='UNMAPPED':raise ValueError(r)
 r['source_record']=r.get('source_record') or ('docs/reconstructions.json' if r['id'] in recon else 'docs/lung-production-adaptation.json' if r['id'].startswith('MAL_') else 'docs/authored-lumens.json' if 'authored_lumen' in r['source_kind'] else 'source/thorax-selection.json')
(ROOT/'exports/anatomy-catalog.json').write_text(json.dumps({'records':catalog},indent=2))
fields=['id','anatomical_identifier','name','required_group','system','tissue','source_kind','source_record','mesh_ids','cutting_ready','reference_region','notes']
with (ROOT/'docs/structure-inventory.csv').open('w',newline='',encoding='utf-8-sig') as f:
 writer=csv.DictWriter(f,fieldnames=fields,extrasaction='ignore');writer.writeheader()
 for r in catalog:writer.writerow({**r,'mesh_ids':'; '.join(r.get('mesh_ids',[]))})
summary={'total_entries':len(catalog),'tissue_entries':sum(not r.get('reference_region',False) for r in catalog),'non_tissue_annotations':sum(r.get('reference_region',False) for r in catalog),'cuttable':sum(r['cutting_ready'] for r in catalog),'groups':dict(sorted(collections.Counter(r['required_group'] for r in catalog).items())),'source_kinds':dict(collections.Counter(r['source_kind'] for r in catalog))}
(ROOT/'qa/reports/inventory.json').write_text(json.dumps(summary,indent=2));print(json.dumps(summary,indent=2))
