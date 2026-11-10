$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$manifest = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'learning-narration.json') | ConvertFrom-Json
$voiceRoot = Join-Path $projectRoot 'unity/ThoraxLab/Assets/ThoraxLab/Resources/Learning/Voice'
New-Item -ItemType Directory -Force -Path $voiceRoot | Out-Null
Add-Type -AssemblyName System.Speech
$speaker = New-Object System.Speech.Synthesis.SpeechSynthesizer
$speaker.SelectVoice($manifest.voice)
$speaker.Rate = $manifest.rate
if ($speaker.Voice.Gender -ne [System.Speech.Synthesis.VoiceGender]::Female) { $speaker.Dispose(); throw 'The learning guide requires an installed female voice.' }
try {
    foreach ($clip in $manifest.clips) {
        $clipPath = Join-Path $voiceRoot ($clip.id + '.wav')
        $clipHashPath = $clipPath + '.sha256'
        $clipTextBytes = [Text.Encoding]::UTF8.GetBytes($manifest.voice + "`n" + $manifest.rate + "`n" + $clip.text)
        $clipHasher = [Security.Cryptography.SHA256]::Create()
        try { $clipHash = [BitConverter]::ToString($clipHasher.ComputeHash($clipTextBytes)).Replace('-', '').ToLowerInvariant() }
        finally { $clipHasher.Dispose() }
        if ((Test-Path -LiteralPath $clipPath) -and (Test-Path -LiteralPath $clipHashPath) -and ((Get-Content -LiteralPath $clipHashPath -Raw).Trim() -eq $clipHash)) { continue }
        $speaker.SetOutputToWaveFile($clipPath)
        $speaker.Speak($clip.text)
        $speaker.SetOutputToNull()
        Set-Content -LiteralPath $clipHashPath -Value $clipHash -Encoding ascii -NoNewline
    }
} finally { $speaker.Dispose() }
Write-Output ('Bundled narration clips: ' + (Get-ChildItem -LiteralPath $voiceRoot -Filter '*.wav').Count)
