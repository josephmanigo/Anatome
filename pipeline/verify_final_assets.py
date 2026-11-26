"""Run the final export and anatomy contract checks sequentially in Blender."""
import runpy
from pathlib import Path
folder=Path(__file__).resolve().parent
for script in ['validate_anatomy_native.py','finalize_anatomy.py','export_lods.py','apply_reviewed_models.py','validate_roundtrip.py','test_anatomy_cuts.py','check_cavities.py']:
 print('VERIFY_STEP',script,flush=True);runpy.run_path(str(folder/script),run_name='__main__')
print('FINAL_ASSET_VERIFICATION_COMPLETE',flush=True)
