# Generates each voice line with Chatterbox Multilingual (MIT), cloned from the owner's own recording (ref.wav).
import sys, json, torch, torchaudio, os
from chatterbox.mtl_tts import ChatterboxMultilingualTTS
here = os.path.dirname(os.path.abspath(__file__))
LINES = json.load(open(os.path.join(here, 'lines.json'), encoding='utf-8'))
only = set(sys.argv[1:])
torch.manual_seed(7)
m = ChatterboxMultilingualTTS.from_pretrained(device='cpu')
for i, text in enumerate(LINES, 1):
    if only and str(i) not in only: continue
    for take in range(int(os.environ.get('TAKES', '1'))):
        torch.manual_seed(7 + take)
        wav = m.generate(text, language_id='ar', audio_prompt_path=os.path.join(here, 'ref.wav'),
                         exaggeration=float(os.environ.get('EXAG', '0.55')), cfg_weight=float(os.environ.get('CFG', '0.4')), temperature=0.7)
        out = os.path.join(here, f'l{i:02d}' + (f'-t{take}' if take else '') + '.wav')
        torchaudio.save(out, wav, m.sr)
        print('done', i, take, round(wav.shape[-1] / m.sr, 2), flush=True)
