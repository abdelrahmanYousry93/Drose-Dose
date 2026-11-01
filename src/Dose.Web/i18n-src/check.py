#!/usr/bin/env python3
"""Fails if a translation key used in the app is missing from the dictionaries (static keys only)."""
import json, glob, os, re, sys
here = os.path.dirname(os.path.abspath(__file__))
keys = set()
for f in glob.glob(os.path.join(here, '*.json')): keys |= set(json.load(open(f, encoding='utf-8')))
prefixes = {k.rsplit('.', 1)[0] + '.' for k in keys}
used = set()
for f in glob.glob(os.path.join(here, '..', 'src', 'app', '**', '*.*'), recursive=True):
    if '/i18n/' in f or not f.endswith(('.ts', '.html')): continue
    s = open(f, encoding='utf-8').read()
    used |= set(re.findall(r"'([a-zA-Z]+(?:\.[a-zA-Z_]+)+)'\s*\|\s*t\b", s))
    used |= set(re.findall(r"\b(?:translate|tr|translateIn)\((?:[a-z]+, )?'([a-zA-Z]+(?:\.[a-zA-Z_]+)+)'", s))
    used |= set(re.findall(r"\?\s*'([a-zA-Z]+\.[a-zA-Z_.]+)'\s*:\s*'([a-zA-Z]+\.[a-zA-Z_.]+)'", s) and
                [k for pair in re.findall(r"\?\s*'([a-zA-Z]+\.[a-zA-Z_.]+)'\s*:\s*'([a-zA-Z]+\.[a-zA-Z_.]+)'", s) for k in pair])
    used |= set(re.findall(r"title: '(titles\.[a-zA-Z]+)'", s))
missing = sorted(k for k in used if k not in keys and not k.endswith('.'))
print(f'{len(used)} keys used, {len(keys)} defined')
if missing: print('MISSING:', *missing, sep='\n  '); sys.exit(1)
