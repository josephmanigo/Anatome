import hashlib
import json
import os
from pathlib import Path
import shutil
import uuid

root = Path(os.environ['USERPROFILE']) / 'AppData/LocalLow/Thorax Lab/Thorax Lab/Learning-v1'
repaired = 0
for name in ('settings.save', 'settings.save.bak'):
    path = root / name
    if not path.exists():
        continue
    magic, checksum, body = path.read_bytes().split(b'\n', 2)
    assert magic == b'THORAX-SAVE-1'
    assert hashlib.sha256(body).hexdigest().encode() == checksum
    data = json.loads(body)
    changed = False
    for profile in data['profiles']:
        if profile['schemaVersion'] == 2:
            profile['schemaVersion'] = 1
            changed = True
    if not changed:
        continue
    backup = path.with_name(name + '.grab-walk-test-backup')
    assert not backup.exists(), 'Preserve the previous test backup.'
    shutil.copy2(path, backup)
    encoded = json.dumps(data, ensure_ascii=False, separators=(',', ':'), allow_nan=False).encode('utf-8')
    temporary = path.with_name(name + '.' + uuid.uuid4().hex + '.tmp')
    temporary.write_bytes(magic + b'\n' + hashlib.sha256(encoded).hexdigest().encode() + b'\n' + encoded)
    os.replace(temporary, path)
    restored = path.read_bytes().split(b'\n', 2)
    assert hashlib.sha256(restored[2]).hexdigest().encode() == restored[1]
    assert all(p['schemaVersion'] == 1 for p in json.loads(restored[2])['profiles'])
    repaired += 1
print(f'Restored cloud-compatible schema in {repaired} local test preference files; checksums verified and originals preserved.')
