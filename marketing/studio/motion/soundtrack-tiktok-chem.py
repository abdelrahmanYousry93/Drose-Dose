"""Synthesised soundtrack for the parents ad (no third-party audio): a tense minor pulse for the problem part,
a riser into the Dose reveal, then a brighter major groove; plus SFX synced to the animation."""
import sys, wave, numpy as np
import json
SR = 44100
DUR = float(sys.argv[2]) if len(sys.argv) > 2 else 16.0
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



def click(): n = int(.03*SR); return np.random.randn(n)*np.exp(-np.arange(n)/SR*200)*0.5
BPM = 112; beat = 60/BPM
chords = [[261.6, 329.6, 392], [196, 246.9, 293.7], [220, 261.6, 329.6], [174.6, 220, 261.6]]
t = 0.0; k = 0
while t < DUR - 0.5:
    add(kick(), t, .55 if k % 2 == 0 else .35); add(hat(), t + beat/2, .4)
    if k % 2 == 1: add(snap(), t, .3)
    t += beat; k += 1
t = 0.0; i = 0
while t < DUR - 0.5:
    d = min(beat*4, DUR - t); add(pad(chords[i % 4], d, .09), t); add(tone(chords[i % 4][0]/2, d, .02, d*.8), t, .25)
    for st in range(8):
        if t + st*beat/2 < DUR - .5: add(tone(chords[i % 4][st % 3]*2, .22, .005, .1), t + st*beat/2, .07)
    t += beat*4; i += 1
add(whoosh(.35), 0.0, .4); add(pop(660), 0.05, .5); add(pop(520), 0.9, .4)
for i in range(3):
    s0 = 2.4 + i*3.93; t0 = s0 + .45
    add(whoosh(.35), s0 - .1, .4); add(pop(880), s0 + .2, .4)
    for k in range(6): add(click(), t0 + k*.5, .9 if k >= 4 else .6)
    add(ding(), t0 + 3, .7)
add(whoosh(.4), 14.1, .4); add(pop(740), 14.6, .5)
add(whoosh(.4), 16.6, .4); add(kick(), 17.7, 1.0); add(ding(), 17.8, .7); add(ding(), 18.9, .5)
out[:int(.03*SR)] *= np.linspace(0, 1, int(.03*SR)); fo = int(1.0*SR); out[-fo:] *= np.linspace(1, 0, fo)
out = out / (np.max(np.abs(out)) + 1e-9) * 0.85
with wave.open(sys.argv[1], 'w') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((out*32767).astype(np.int16).tobytes())
print('wrote')
