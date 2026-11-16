"""Fills the voice timings and caption lines into dose-not-subscription.html -> dose-not-subscription.built.html."""
import json, os
here = os.path.dirname(os.path.abspath(__file__))
t = json.load(open(os.path.join(here, 'dose-not-subscription.times.json')))
CAPS = ["لما راسك بتوجعك…", "بتاخد قرص؟ ولا بتشترك في الصيدلية بالشهر؟", "أكيد… قرص طبعًا.",
        "طب ليه في الدروس… بتدفع شهر كامل؟", "علشان درس واحد بس واقف فيه!", "في دروس Dose… بتاخد جرعتك وبس.",
        "اختار الدرس اللي واقف معاك…", "احجز جرعة شرح واحدة…", "فهمته؟ خلاص. مفيش اشتراك.",
        "إيه الدرس اللي واقف معاك؟ اكتبه في الكومنت.", "دروس Dose… جرعة فهم، مش اشتراك شهر."]
html = open(os.path.join(here, 'dose-not-subscription.html'), encoding='utf-8').read()
html = html.replace('/*SEGS*/', json.dumps(t['segs'])).replace('/*END*/', str(t['dur'])).replace('/*LINES*/', json.dumps(CAPS, ensure_ascii=False))
open(os.path.join(here, 'dose-not-subscription.built.html'), 'w', encoding='utf-8').write(html)
print('built', t['dur'])
