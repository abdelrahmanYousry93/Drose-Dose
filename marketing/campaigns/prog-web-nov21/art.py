"""Illustration for the programming poster: a delighted parent fanning the money he saved, next to his son with a laptop."""


def note(x, y, rot, s=1.0):
    return f'''<g transform="translate({x} {y}) rotate({rot}) scale({s})">
  <rect x="-62" y="-32" width="124" height="64" rx="8" fill="#2fb36b" stroke="#1d7a47" stroke-width="4"/>
  <rect x="-52" y="-22" width="104" height="44" rx="5" fill="none" stroke="#bff0d4" stroke-width="3"/>
  <circle cx="0" cy="0" r="15" fill="#bff0d4"/><text x="0" y="6" font-family="Cairo" font-weight="900" font-size="17" text-anchor="middle" fill="#1d7a47">100</text>
</g>'''


def coin(x, y, r=20):
    return f'<g><circle cx="{x}" cy="{y}" r="{r}" fill="#FFC30A" stroke="#c98a00" stroke-width="4"/><text x="{x}" y="{y + r * .35}" font-family="Cairo" font-weight="900" font-size="{r}" text-anchor="middle" fill="#a86f00">ج</text></g>'


def spark(x, y, s=1, c='#fff'):
    return f'<path transform="translate({x} {y}) scale({s})" d="M0-18 4-4 18 0 4 4 0 18-4 4-18 0-4-4z" fill="{c}"/>'


def art():
    fan = ''.join(note(640 + dx, 150 + dy, r) for dx, dy, r in [(-70, 20, -38), (-30, -10, -18), (15, -20, 2), (60, -8, 22), (95, 18, 40)])
    return f'''<svg viewBox="0 0 900 640" xmlns="http://www.w3.org/2000/svg">
  <ellipse cx="450" cy="610" rx="360" ry="26" fill="rgba(11,31,107,.18)"/>
  <!-- floating money -->
  {note(130, 120, -24, .8)}{note(820, 330, 28, .75)}{note(210, 300, 16, .6)}
  {coin(90, 260)}{coin(800, 150, 24)}{coin(760, 470, 18)}{coin(300, 80, 16)}
  {spark(520, 60, 1.4)}{spark(860, 230, 1)}{spark(60, 420, 1.1)}{spark(380, 190, .8, '#FFC30A')}

  <!-- son (left): hoodie + laptop + thumbs up -->
  <g transform="translate(170 250)">
    <path d="M30 360c4-120 50-170 120-170s116 50 120 170z" fill="#7c3aed"/>
    <path d="M110 196l40 40 40-40" fill="none" stroke="#5b21b6" stroke-width="10" stroke-linecap="round"/>
    <circle cx="150" cy="120" r="74" fill="#e9b88f"/>
    <path d="M76 112c0-50 32-80 74-80s76 28 74 80c-14-24-40-34-74-34s-60 10-74 34z" fill="#1d130d"/>
    <path d="M118 120q12-14 24 0M158 120q12-14 24 0" fill="none" stroke="#1d130d" stroke-width="7" stroke-linecap="round"/>
    <path d="M116 146q34 34 68 0z" fill="#fff" stroke="#1d130d" stroke-width="5" stroke-linejoin="round"/>
    <circle cx="104" cy="148" r="11" fill="#f39b8a" opacity=".55"/><circle cx="196" cy="148" r="11" fill="#f39b8a" opacity=".55"/>
    <!-- laptop -->
    <g transform="translate(70 250)"><rect x="0" y="0" width="170" height="112" rx="10" fill="#0B1F6B" stroke="#e8edf7" stroke-width="6"/>
      <text x="85" y="72" font-family="DejaVu Sans Mono" font-weight="700" font-size="44" text-anchor="middle" fill="#2fe08a">&lt;/&gt;</text>
      <rect x="-16" y="110" width="202" height="14" rx="7" fill="#e8edf7"/></g>
    <!-- thumbs up -->
    <g transform="translate(262 236)"><rect x="-14" y="10" width="40" height="44" rx="12" fill="#e9b88f" stroke="#c98d63" stroke-width="3"/><rect x="0" y="-24" width="16" height="40" rx="8" fill="#e9b88f" stroke="#c98d63" stroke-width="3"/></g>
  </g>

  <!-- parent (right): beaming, glasses, mustache, fanning the notes -->
  <g transform="translate(470 170)">
    <path d="M20 440c6-150 70-210 160-210s154 60 160 210z" fill="#0061FA"/>
    <path d="M140 232l40 56 40-56" fill="#fff"/><path d="M172 252h16l6 80-14 18-14-18z" fill="#FFC30A"/>
    <!-- arms up to the money fan -->
    <path d="M50 300C30 230 60 170 110 130" fill="none" stroke="#0061FA" stroke-width="46" stroke-linecap="round"/>
    <path d="M310 300c22-70 0-130-40-170" fill="none" stroke="#0061FA" stroke-width="46" stroke-linecap="round"/>
    <circle cx="112" cy="122" r="26" fill="#e3a77c"/><circle cx="268" cy="126" r="26" fill="#e3a77c"/>
    <circle cx="180" cy="160" r="86" fill="#e3a77c"/>
    <path d="M96 150c-4-62 34-98 84-98s90 34 84 98c-10-30-40-48-84-48s-74 18-84 48z" fill="#3b2a1e"/>
    <path d="M120 150c8-6 30-6 44 0M196 150c14-6 36-6 44 0" fill="none" stroke="#3b2a1e" stroke-width="8" stroke-linecap="round"/>
    <circle cx="142" cy="172" r="22" fill="none" stroke="#0B1F6B" stroke-width="6"/><circle cx="218" cy="172" r="22" fill="none" stroke="#0B1F6B" stroke-width="6"/><path d="M164 172h32" stroke="#0B1F6B" stroke-width="6"/>
    <path d="M130 174q12-14 24 0M206 174q12-14 24 0" fill="none" stroke="#1d130d" stroke-width="6" stroke-linecap="round"/>
    <path d="M140 204q40 22 80 0q-6 10-40 10t-40-10z" fill="#3b2a1e"/>
    <path d="M146 214q34 44 68 0z" fill="#fff" stroke="#1d130d" stroke-width="5" stroke-linejoin="round"/><path d="M160 232q20 14 40 0" fill="#ff7a7a"/>
    <circle cx="112" cy="196" r="13" fill="#f08a7a" opacity=".5"/><circle cx="248" cy="196" r="13" fill="#f08a7a" opacity=".5"/>
  </g>
  {fan}
</svg>'''
