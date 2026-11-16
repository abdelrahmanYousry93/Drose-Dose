"""Synthesised soundtrack for the "dose, not a subscription" ad (no third-party audio): a light curious pulse for the
poll hook, tension on the monthly-subscription part, a riser into the Dose reveal, then a bright groove; SFX synced."""
import sys, wave, numpy as np
import json
SR = 44100
CFG = json.load(open(sys.argv[2])); seg = CFG["segs"]; DUR = CFG["dur"] + 0.3
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
def thud():
    k = kick()*1.2; s = snap()*0.6; k[:len(s)] += s; return k
L = lambda i, e=0: seg[i-1][e]
BPM = 104; beat = 60/BPM
TURN = L(6) - 0.05
# Part 1: curious pluck pulse (hook) then tense minor (subscription)
chords_a = [[261.6, 329.6, 392], [220, 261.6, 329.6]]
chords_min = [[220, 261.6, 329.6], [174.6, 220, 261.6], [196, 246.9, 293.7], [164.8, 207.7, 246.9]]
t = 0.0; k = 0
while t < TURN - 0.2:
    tense = t >= L(4) - 0.2
    add(kick(), t, (.45 if k % 2 == 0 else .3) if tense else (.3 if k % 2 == 0 else 0))
    add(hat(), t + beat/2, .35)
    if tense and k % 4 == 3: add(snap(), t, .3)
    if not tense: add(tone([523.3, 659.3, 784, 659.3][k % 4], .2, .003, .1), t, .12)
    t += beat; k += 1
t = L(4) - 0.2; i = 0
while t < TURN - 0.4:
    d = min(beat*4, TURN - t); add(pad(chords_min[i % 4], d, .10), t); add(tone(chords_min[i % 4][0]/2, d, .02, d*0.8), t, .25); t += beat*4; i += 1
n = int(1.0*SR); add(np.sin(2*np.pi*np.cumsum(np.linspace(200, 900, n))/SR)*np.linspace(0, .22, n), TURN-1.0)
add(whoosh(.9, True), TURN-0.9, .6)
add(kick(), TURN, 1.0); add(tone(110, 1.2, .005, .6), TURN, .6); add(ding(), TURN + .1, .8)
chords_maj = [[261.6, 329.6, 392], [196, 246.9, 293.7], [220, 261.6, 329.6], [174.6, 220, 261.6]]
t = TURN; k = 0
while t < DUR - 0.6:
    add(kick(), t, .5); add(hat(), t + beat/2, .45); add(hat(), t + beat*0.75, .2)
    if k % 2 == 1: add(snap(), t, .35)
    t += beat; k += 1
t = TURN; i = 0
while t < DUR - 0.6:
    d = min(beat*4, DUR - t); add(pad(chords_maj[i % 4], d, .10), t); add(tone(chords_maj[i % 4][0]/2, d, .02, d*0.8), t, .28)
    for s in range(8):
        if t + s*beat/2 < DUR - 0.5: add(tone(chords_maj[i % 4][s % 3]*2, .25, .005, .12), t + s*beat/2, .08)
    t += beat*4; i += 1
# SFX
add(whoosh(.35), L(1) + .1, .3)
add(pop(660), L(2), .55)                                   # poll appears
add(click(), L(3) - 0.05, .9); add(ding(), L(3) + .15, .6)  # tap pill -> right
add(buzz(.35), L(3) + .35, .5)                             # subscription -> wrong
add(whoosh(.4), L(4) - .3, .4)
for i in range(30): add(click(), L(4) + .25 + (i+1)*0.03, .35)   # 30 days fill
add(thud(), L(4, 1) - .25, .7)                             # "paid" stamp
add(pop(520), L(5), .55)
for at in [L(7), L(8), L(9)]: add(whoosh(.3), at - .1, .3); add(pop(880), at + .05, .45)
add(thud(), L(9) + .8, .6); add(ding(), L(9) + .9, .5)
for i in range(4): add(pop(990 + i*120), L(10) + .5 + i*.4, .35)
tt = L(10) + .4
while tt < L(10, 1) + .5: add(click(), tt, .25); tt += 0.09
add(whoosh(.4), L(11) - .25, .4); add(pop(740), L(11), .5); add(ding(), L(11, 1) + .1, .6)
out[:int(.05*SR)] *= np.linspace(0, 1, int(.05*SR)); fo = int(1.2*SR); out[-fo:] *= np.linspace(1, 0, fo)
out = out / (np.max(np.abs(out)) + 1e-9) * 0.89
with wave.open(sys.argv[1], 'w') as w:
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((out*32767).astype(np.int16).tobytes())
print('wrote', sys.argv[1])
