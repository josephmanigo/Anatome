"""Check the 249 models and their three triangle budgets in the actual Android player data."""
import csv
import hashlib
import json
import re
import sys
import zipfile
from collections import defaultdict
from pathlib import Path

import UnityPy


def inventory(apk):
    prefix = 'assets/bin/Data/sharedassets0.assets'
    with zipfile.ZipFile(apk) as z:
        parts = sorted((int(m.group(1) or 0), name) for name in z.namelist()
                       if (m := re.fullmatch(re.escape(prefix) + r'(?:\.split(\d+))?', name)))
        if not parts:
            raise ValueError('Unity shared scene assets missing')
        data = b''.join(z.read(name) for _, name in parts)
    environment = UnityPy.load(data)
    result = defaultdict(list)
    for obj in environment.objects:
        if obj.type.name != 'Mesh':
            continue
        mesh = obj.read()
        xyz = mesh.m_LocalAABB.m_Extent
        count = sum(sub.indexCount for sub in mesh.m_SubMeshes)
        if count % 3:
            raise ValueError('Unexpected non-triangle index count for ' + mesh.m_Name)
        result[mesh.m_Name].append({
            'triangles': count // 3,
            'boundsSize': [round(2 * n, 7) for n in (xyz.x, xyz.y, xyz.z)],
            'indicesSha256': hashlib.sha256(bytes(mesh.m_IndexBuffer)).hexdigest(),
        })
    for variants in result.values():
        variants.sort(key=lambda mesh: mesh['triangles'], reverse=True)
    return result


def verify(apk, previous, table, output):
    catalog = list(csv.DictReader(table.open(encoding='utf-8')))
    if len(catalog) != 249:
        raise ValueError('Expected the complete anatomical catalog')
    actual, earlier = inventory(apk), inventory(previous)
    distinct = set()
    totals = [0, 0, 0]
    changes = {}
    for row in catalog:
        name = row['mesh']
        current, old = actual[name], earlier[name]
        expected = [int(row[f'lod{i}']) for i in range(3)]
        if [m['triangles'] for m in current] != expected or len(old) != 3:
            raise ValueError(f'Packaged detail levels differ from the catalog for {name}')
        for level, count in enumerate(expected):
            totals[level] += count
        if current != old:
            distinct.add(name)
            changes[name] = [{'previousTriangles': o['triangles'], 'currentTriangles': n['triangles'],
                              'previousSize': o['boundsSize'], 'currentSize': n['boundsSize']}
                             for o, n in zip(old, current)]
    corrected = {'A_FMA7396','A_FMA50872','A_FMA7240','A_FMA7265','A_FMA9756','A_FMA9757'}
    if distinct != corrected:
        raise ValueError(f'Unexpected mesh differences: {sorted(distinct ^ corrected)}')
    for name in ('A_FMA7396','A_FMA50872'):
        for pair in changes[name]:
            if not any(n < o * 0.9 for n,o in zip(pair['currentSize'],pair['previousSize'])):
                raise ValueError(f'Expected the repaired extent to contract for {name}')
    report = dict(apk=apk.name, apkSha256=hashlib.sha256(apk.read_bytes()).hexdigest(),
                  previousApk=previous.name, catalogEntries=len(catalog), detailLevels=3,
                  verifiedPackagedMeshes=3*len(catalog), packagedTrianglesByDetail=totals,
                  unchangedBoundsAndTriangleIndices=len(catalog)-len(distinct), corrected=changes)
    output.write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print(f"Verified {report['verifiedPackagedMeshes']} packaged meshes. Six catalog entries changed.",flush=True)


if __name__ == '__main__':
    apk, previous, output = map(Path, sys.argv[1:])
    verify(apk, previous, Path(__file__).with_name('expected-models.csv'), output)
