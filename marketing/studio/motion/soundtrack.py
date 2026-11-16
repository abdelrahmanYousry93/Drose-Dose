"""Synthesised soundtrack for the parents ad (no third-party audio): a tense minor pulse for the problem part,
a riser into the Dose reveal, then a brighter major groove; plus SFX synced to the animation."""
import sys, wave, numpy as np
SR = 44100; DUR = float(sys.argv[2]) if len(sys.argv) > 2 else 34.0
N = int(SR * DUR); out = np.zeros(N)
t_all = np.arange(N) / SR
def add(sig, at, gain=1.0):
    i = int(at * SR); j = min(N, i + len(sig))
    if i < N: out[i:j] += sig[: j - i] * gain
def env(n, a=0.005, r=0.2):
    e = np.ones(n); na = max(1, int(a * SR)); e[:na] = np.linspace(0, 1, na)
    t = np.arange(n) / SR; return e * np.exp(-t / max(r, 1e-3))
def tone(f, d, a=0.01, r=0.3, kind='sine'):
    n = int(d * SR); t = np.arange(n) / SR
    w = np.sin(2*np.pi*f*t) if kind == 'sine' else np.sign(np.sin(2*np.pi*f*t)) * 0.5
    return w * env(n, a, r)
def kick(): n = int(.35*SR); t = np.arange(n)/SR; f = 110*np.exp(-t*18)+45; return np.sin(2*np.pi*np.cumsum(f)/SR)*np.exp(-t*9)
def hat(): n = int(.06*SR); return np.random.randn(n)*np.exp(-np.arange(n)/SR*70)*0.25
def snap(): n = int(.18*SR); t=np.arange(n)/SR; return (np.random.randn(n)*0.5+np.sin(2*np.pi*190*t))*np.exp(-t*28)
def pad(freqs, d, gain=.12):
    n = int(d*SR); t = np.arange(n)/SR; w = sum(np.sin(2*np.pi*f*t)+0.3*np.sin(2*np.pi*2*f*t+0.3) for f in freqs)/len(freqs)
    e = np.minimum(1, t/0.4) * np.minimum(1, (d - t)/0.6); return w*e*gain
def whoosh(d=0.45, up=True):
    n = int(d*SR); x = np.random.randn(n); y = np.zeros(n); a = 0
    cut = np.linspace(0.02, 0.35, n) if up else np.linspace(0.35, 0.02, n)
    for i in range(n): a += cut[i]*(x[i]-a); y[i] = a
    return y*np.sin(np.linspace(0, np.pi, n))*0.9
def pop(f=880): return tone(f, .16, .002, .05)+tone(f*1.5, .16, .002, .04)*0.5
def buzz(d=0.5): n=int(d*SR); t=np.arange(n)/SR; return np.sign(np.sin(2*np.pi*150*t))*0.25*(np.sin(2*np.pi*12*t)>0)*np.minimum(1,(d-t)/0.05)
def ding(): return tone(1318, .5, .002, .25)*0.5 + tone(1760, .5, .002, .2)*0.35

BPM = 100; beat = 60/BPM
TURN = 19.85  # Dose reveal (16.65 + 3.2)
# --- problem part: tense minor pulse (A minor / F) from 3.2 to TURN
t = 3.2; k = 0
while t < TURN - 0.2:
    add(kick(), t, .55 if k % 2 == 0 else .35); add(hat(), t + beat/2, .5)
    if k % 4 == 3: add(snap(), t, .35)
    t += beat; k += 1
chords_min = [[220, 261.6, 329.6], [174.6, 220, 261.6], [196, 246.9, 293.7], [164.8, 207.7, 246.9]]
t = 3.2; i = 0
while t < TURN - 0.5:
    d = min(beat*4, TURN - t); add(pad(chords_min[i % 4], d, .10), t); add(tone(chords_min[i % 4][0]/2, d, .02, d*0.8), t, .25); t += beat*4; i += 1
# riser into the reveal
n = int(1.2*SR); tt = np.arange(n)/SR; add(np.sin(2*np.pi*np.cumsum(np.linspace(200, 900, n))/SR)*np.linspace(0, .25, n), TURN-1.2)
add(whoosh(1.0, True), TURN-1.0, .7)
# --- reveal hit
add(kick(), TURN, 1.0); add(tone(110, 1.2, .005, .6), TURN, .6); add(ding(), TURN+0.85, .9)
# --- solution part: brighter major groove
chords_maj = [[261.6, 329.6, 392], [196, 246.9, 293.7], [220, 261.6, 329.6], [174.6, 220, 261.6]]
t = TURN; k = 0
while t < DUR - 0.6:
    add(kick(), t, .6); add(hat(), t + beat/2, .6); add(hat(), t + beat*0.75, .3)
    if k % 2 == 1: add(snap(), t, .45)
    t += beat; k += 1
t = TURN; i = 0
while t < DUR - 0.6:
    d = min(beat*4, DUR - t); add(pad(chords_maj[i % 4], d, .11), t); add(tone(chords_maj[i % 4][0]/2, d, .02, d*0.8), t, .3)
    for s in range(8):  # light arpeggio
        if t + s*beat/2 < DUR - 0.5: add(tone(chords_maj[i % 4][s % 3]*2, .25, .005, .12), t + s*beat/2, .10)
    t += beat*4; i += 1
# --- SFX synced to the animation (times include the 3.2 s hook shift)
add(buzz(), 0.45, .8); add(ding(), 0.5, .5); add(buzz(), 2.25, .8); add(ding(), 2.3, .5); add(pop(660), 1.45, .7)
for at in [3.25, 7.2, 11.8, 16.4, 23.0, 28.4]: add(whoosh(.4), at - 0.15, .45)
for at in [7.8, 8.15, 8.5, 8.85, 9.2]: add(pop(990), at, .35)                       # subject chips
for at in [7.9, 8.25, 8.6, 8.95, 9.3]: add(kick(), at + .25, .25)  # bills landing
add(pop(520), 12.3, .5); add(pop(740), 17.6, .6)
for at in [23.6, 24.4, 25.2]: add(pop(880), at, .5)
for at in [26.2, 26.5, 26.8]: add(pop(1175), at, .35)
add(pop(660), 28.55, .5); add(pop(990), 29.5, .6); add(ding(), 30.2, .7)
# master: gentle fade in/out, normalise
out[:int(.05*SR)] *= np.linspace(0, 1, int(.05*SR)); fo = int(1.2*SR); out[-fo:] *= np.linspace(1, 0, fo)
out = out / (np.max(np.abs(out)) + 1e-9) * 0.89
with wave.open(sys.argv[1], 'w') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((out*32767).astype(np.int16).tobytes())
print('wrote', sys.argv[1])
