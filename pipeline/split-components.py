from pathlib import Path
p=Path(__file__).resolve().parents[1]/'unity/ThoraxLab/Assets/ThoraxLab/Scripts/LabControls.cs'
s=p.read_text();header=s[:s.index('public class LabButton')]
button=s[s.index('public class LabButton'):s.index('public class LabHandInteractor')]
hand=s[s.index('public class LabHandInteractor'):s.index('public class LabControls')]
p.with_name('LabButton.cs').write_text(header+button+'}\n')
p.with_name('LabHandInteractor.cs').write_text(header+hand+'}\n')
p.write_text(header+s[s.index('public class LabControls'):])
