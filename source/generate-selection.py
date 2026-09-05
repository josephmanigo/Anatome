"""Resolve teaching-thorax concepts without duplicating BodyParts3D elements.

This is an inventory, not an anatomical validation. Specific ISA identities take
precedence over PART-OF compounds, which have documented mapping ambiguities.
"""
from __future__ import annotations
import csv
import hashlib
import json
import re
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'source'
META = SOURCE / 'bodyparts3d/metadata'
names, elements = {}, defaultdict(set)
for tree in ('isa', 'partof'):
    for row in csv.DictReader((META / (tree + '_parts_list_e.txt')).open(encoding='utf-8-sig'), delimiter='\t'):
        names[(tree, row['concept id'])] = row['en']
    for row in csv.DictReader((META / (tree + '_element_parts.txt')).open(encoding='utf-8-sig'), delimiter='\t'):
        elements[(tree, row['concept id'])].add(row['element file id'])

records, used, conflicts = [], set(), []

def add(concept, system, layer, tissue, group, tree='isa', label=None, **extras):
    key = tree, concept
    if key not in names:
        raise ValueError(f'Missing concept: {key}')
    source_elements = elements[key]
    shared = source_elements & used
    chosen = source_elements - used
    if shared:
        conflicts.append({'concept': concept, 'tree': tree, 'previously_assigned_elements': sorted(shared)})
    if not chosen:
        return
    record = {'id': concept, 'name': label or names[key], 'mesh_ids': sorted(chosen),
              'system': system, 'layer': layer, 'tissue': tissue, 'required_group': group,
              'source_kind': 'source_mesh', 'source_tree': tree,
              'source_name': names[key], 'source_record': 'source/source-manifest.json',
              'license': 'CC-BY-4.0', 'source_concept_mesh_ids': sorted(source_elements), **extras}
    records.append(record)
    used.update(chosen)

def match(pattern, system, layer, tissue, group, **extras):
    for (tree, concept), name in names.items():
        if tree == 'isa' and re.fullmatch(pattern, name):
            add(concept, system, layer, tissue, group, **extras)

ordinal = r'(?:first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|eleventh|twelfth)'
match(r'(?:right|left) ' + ordinal + ' rib', 'Skeletal', 3, 'bone', 'ribs')
match(r'(?:right|left) ' + ordinal + ' costal cartilage', 'Skeletal', 3, 'cartilage', 'costal_cartilages')
for concept in ('FMA7486', 'FMA7487', 'FMA7488'):
    add(concept, 'Skeletal', 3, 'bone', 'sternum', tree='isa' if ('isa', concept) in names else 'partof')
match(ordinal + ' thoracic vertebra', 'Skeletal', 6, 'bone', 'thoracic_vertebrae')
match('intervertebral disk of ' + ordinal + ' thoracic vertebra', 'Skeletal', 6, 'cartilage', 'thoracic_discs')
match(r'(?:right|left) (?:clavicle|scapula)', 'Skeletal', 3, 'bone', 'shoulder_girdle')
match(r'(?:clavicular|sternocostal|abdominal) part of (?:right|left) pectoralis major', 'Muscular', 2, 'muscle', 'pectoralis_major')
match(r'(?:right|left) pectoralis minor', 'Muscular', 2, 'muscle', 'pectoralis_minor')
match(r'(?:right|left) serratus anterior', 'Muscular', 2, 'muscle', 'serratus_anterior')
for concept in ('FMA9756', 'FMA9757', 'FMA9758', 'FMA9761', 'FMA9762'):
    add(concept, 'Muscular', 2, 'muscle', 'intercostal_layers')
add('FMA13295', 'Muscular', 5, 'muscle', 'diaphragm')
add('FMA7163', 'Integumentary', 0, 'skin', 'thoracic_skin', regional_crop_required=True)

add('FMA7394', 'Respiratory', 5, 'airway', 'trachea', regional_crop_required=True,
    lumen_status='Not established by inventory; validate topology before cutting')
add('FMA68418', 'Respiratory', 5, 'airway', 'main_bronchi')
add('FMA7396', 'Respiratory', 5, 'airway', 'main_bronchi')
for concept in ('FMA31744', 'FMA31741', 'FMA31747', 'FMA31746', 'FMA31748'):
    add(concept, 'Respiratory', 5, 'airway', 'lobar_bronchial_trees', tree='partof',
        mapping_status='Compound branches; not a standalone lobar-bronchus wall')
# The ISA segment tree has some branch elements omitted by PART-OF lobe compounds.
add('FMA12224', 'Respiratory', 5, 'airway', 'remaining_airway_branches',
    label='Additional source tracheobronchial segments',
    mapping_status='Unassigned remaining airway branches after named groups')

for concept in ('FMA9457', 'FMA9531'):
    add(concept, 'Cardiovascular', 5, 'cardiac', 'atrial_walls')
add('FMA13884', 'Cardiovascular', 5, 'cardiac', 'ventricular_walls',
    mapping_status='Generic shared ventricular wall; individual left/right walls and septa are not separately labeled.',
    cavity_evidence='source/heart-source-geometry-audit.json',
    topology_status='Production cleanup required: exact-position welding diagnostic found seven nonmanifold edges.')
match(r'(?:anterior|posterior|septal) leaflet of tricuspid valve', 'Cardiovascular', 6, 'cardiac', 'tricuspid_valve')
match(r'(?:anterior|posterior) leaflet of mitral valve', 'Cardiovascular', 6, 'cardiac', 'mitral_valve')
match(r'(?:left anterior|right anterior|posterior) cusp of pulmonary valve', 'Cardiovascular', 6, 'cardiac', 'pulmonary_valve')
match(r'(?:right posterior|anterior|left posterior) cusp of aortic valve', 'Cardiovascular', 6, 'cardiac', 'aortic_valve')
match(r'(?:anterior|posterior|septal) papillary muscle of right ventricle', 'Cardiovascular', 6, 'cardiac', 'papillary_muscles')
for concept in ('FMA7264', 'FMA7265'):
    add(concept, 'Cardiovascular', 6, 'cardiac', 'papillary_muscles')
for concept in ('FMA11359', 'FMA9465', 'FMA9291', 'FMA9466'):
    add(concept, 'Cardiovascular', 6, 'cavity', 'cardiac_cavity_reference',
        reference_only=True, cutting_allowed=False,
        note='Cavity boundary/volume reference; not a solid tissue or chamber wall.')

for concept in ('FMA3736', 'FMA3768', 'FMA87217', 'FMA3932', 'FMA3953', 'FMA4694'):
    add(concept, 'Cardiovascular', 5, 'artery', 'great_vessels', regional_crop_required=True)
for concept in ('FMA4720', 'FMA10951', 'FMA4751', 'FMA4761'):
    add(concept, 'Cardiovascular', 5, 'vein', 'great_vessels', regional_crop_required=True)
add('FMA8612', 'Cardiovascular', 5, 'artery', 'pulmonary_vessels')
for concept in ('FMA50872', 'FMA50873'):
    add(concept, 'Cardiovascular', 5, 'artery', 'pulmonary_vessels', tree='partof',
        mapping_status='Artery and intrapulmonary branch assembly')
for concept in ('FMA49911', 'FMA49913', 'FMA49914', 'FMA49916'):
    add(concept, 'Cardiovascular', 5, 'vein', 'pulmonary_vessels', tree='partof',
        mapping_status='Vein and intrapulmonary tributary assembly')
for concept in ('FMA50039', 'FMA50040'):
    add(concept, 'Cardiovascular', 5, 'artery', 'coronary_vessels', tree='partof')
for concept in ('FMA4706', 'FMA4707', 'FMA4709', 'FMA4712'):
    if ('isa', concept) in names:
        add(concept, 'Cardiovascular', 5, 'vein', 'coronary_vessels')
for concept in ('FMA4838', 'FMA4944', 'FMA5011'):
    add(concept, 'Cardiovascular', 6, 'vein', 'azygos_system', regional_crop_required=True)
for concept in ('FMA3969', 'FMA4068', 'FMA66242', 'FMA66243', 'FMA5041', 'FMA4112'):
    add(concept, 'Cardiovascular', 3, 'artery', 'intercostal_vessels')
for concept in ('FMA4758', 'FMA4797', 'FMA4877'):
    add(concept, 'Cardiovascular', 3, 'vein', 'intercostal_vessels')
add('FMA7131', 'Digestive', 6, 'muscle', 'esophagus', regional_crop_required=True)

gray806 = 'https://commons.wikimedia.org/wiki/File:Gray806.png'
gray968 = 'https://commons.wikimedia.org/wiki/File:Gray968.png'
missing = [
    ('subcutaneous_fat', 'Thoracic subcutaneous fat', 'No matching source concept/mesh; reconstruct and cite anatomical cross sections.', gray968),
    ('thoracic_fascia', 'Thoracic fascia', 'No matching source concept/mesh; limb fascia must not be repurposed.', gray968),
    ('lung_lobe_surfaces', 'Five lung lobe tissue surfaces and fissures', 'PART-OF lung-lobe compounds contain airway and vascular elements; no independent parenchymal surface confirmed.', 'https://www.openanatomy.org/atlas-pages/atlas-mauritania-thorax.html'),
    ('pleural_layers', 'Bilateral visceral and parietal pleura', 'No matching source concept/mesh.', gray968),
    ('pericardium', 'Fibrous and serous pericardium', 'No matching source concept/mesh.', gray968),
    ('ventricular_wall_segmentation', 'Separate left and right ventricular wall identities', 'Shared ventricular wall FJ2428 is source-backed and retains sampled chamber voids; named PART-OF left/right wall compounds are unreliable. Separate regional identities require documented segmentation.', 'https://www.nlm.nih.gov/research/visible/visible_human.html'),
    ('cardiac_septa', 'Interatrial and interventricular septa', 'No specific source identity confirmed; cavity references are not septal tissue.', gray968),
    ('vagus_nerves', 'Bilateral vagus nerves', 'No matching source concept/mesh.', gray806),
    ('phrenic_nerves', 'Bilateral phrenic nerves', 'No matching source concept/mesh.', gray806),
    ('sympathetic_trunks', 'Bilateral thoracic sympathetic trunks', 'No matching source concept/mesh.', gray806),
    ('intercostal_nerves', 'Intercostal nerves', 'No matching source concept/mesh.', gray806),
    ('thoracic_duct', 'Thoracic duct', 'No matching source concept/mesh.', gray806),
    ('lower_costal_cartilage', 'Costal cartilage for ribs 8 through 12', 'Catalog has only separately named costal cartilages 1 through 7; inspect lower rib ends before reconstruction.', gray968),
    ('complete_intercostal_vessels', 'Complete intercostal vascular bundles', 'Only first/second posterior arteries and selected thoracic vessels explicitly resolve; full numbered bundle coverage unconfirmed.', gray806),
]

def geometry_info(mesh_id):
    path = SOURCE / 'bodyparts3d/obj' / (mesh_id + '.obj')
    if not path.exists():
        return None
    bounds_min, bounds_max, vertices, triangles = [float('inf')] * 3, [-float('inf')] * 3, 0, 0
    with path.open(encoding='utf-8', errors='strict') as stream:
        for line in stream:
            if line.startswith('v '):
                xyz = [float(x) for x in line.split()[1:4]]
                vertices += 1
                bounds_min = [min(a,b) for a,b in zip(bounds_min,xyz)]
                bounds_max = [max(a,b) for a,b in zip(bounds_max,xyz)]
            elif line.startswith('f '):
                triangles += len(line.split()) - 3
    return {'id':mesh_id, 'vertices':vertices,'triangles':triangles,
            'bounds_mm':{'min':bounds_min, 'max':bounds_max},
            'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}

mesh_info = [info for mesh_id in sorted(used) if (info := geometry_info(mesh_id))]
output = {
    'schema_version':1, 'dataset':'BodyParts3D 4.0 current official IS-A OBJ archive',
    'coordinate_system':{'unit':'mm', 'scale_to_meters':0.001,
        'positive_x':'anatomical left', 'positive_y':'posterior', 'positive_z':'superior',
        'reference':'source/bodyparts3d/metadata/coordinate_system.png',
        'supine_blender_mapping':'(x,y,z) = (source_x, source_z - regional_center_z, -source_y) * 0.001'},
    'records':records,
    'missing_groups':[{'id':id, 'name':name, 'reason':reason, 'status':'requires_authored_reconstruction_or_verified_additional_source',
                       'reference_urls':[url]} for id,name,reason,url in missing],
    'resolution_policy':'Specific ISA leaf meanings first; PART-OF branch assemblies next; never repeat an elemental mesh. Cavity geometry is reference-only.',
    'mapping_conflicts':conflicts, 'selected_element_count':len(used),
    'geometry_readiness':'extracted' if len(mesh_info)==len(used) else 'archive_extraction_pending',
    'element_geometry':mesh_info,
    'limitations':['Source metadata is not independent anatomical validation.',
       'Selected skin and crossing vessels require regional cropping with explicit termination treatment.',
       'Open Anatomy and NLM references have not been spatially registered to the primary specimen.',
       'Documented reduction is 99 percent; smoothing cannot recover missing anatomical detail.']}
if (SOURCE / 'references/open-anatomy/mal-source-manifest.json').exists():
    for group in output['missing_groups']:
        if group['id']=='lung_lobe_surfaces':
            group['status']='verified_independent_source_available_registration_required'
            group['available_source']='source/references/open-anatomy/mal-source-manifest.json'
            group['registration_evidence']='source/references/open-anatomy/coordinate-audit.json'
            group['reason']+=' Five actual MAL lobe meshes have been acquired in separate original coordinates; registration is required.'
(SOURCE / 'thorax-selection.json').write_text(json.dumps(output,indent=2)+'\n',encoding='utf-8')
print(f'{len(records)} teaching records, {len(used)} unique elements, {len(mesh_info)} geometry files ready; {len(missing)} incomplete groups')
