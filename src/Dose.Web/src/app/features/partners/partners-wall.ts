import { Component, OnDestroy, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { Api, PledgeWall } from '../../core/api';
import { TranslatePipe } from '../../core/i18n';

/**
 * The live dose counter for an event screen or a phone held up at a meeting: total doses pledged, the organizations
 * that agreed to be named, and the QR to pledge. Refreshes every 10 seconds; the number counts up when it grows.
 * ?event=ses26 limits it to pledges that came from that event's links.
 */
@Component({
  selector: 'dose-partners-wall',
  imports: [TranslatePipe],
  template: `
    <section class="wall">
      <h1>{{ 'wall.title' | t }}</h1>
      <div class="count"><strong>{{ num(shown()) }}</strong><span>{{ 'wall.unit' | t }}</span></div>
      @if (data(); as w) {
        @if (w.totalDoses > 0) {
          <p class="sub">{{ 'wall.sub' | t: { orgs: w.organizations } }}</p>
        } @else {
          <p class="sub">{{ 'wall.empty' | t }}</p>
        }
        @if (w.names.length) {
          <div class="names">
            <span class="thanks">{{ 'wall.names' | t }}</span>
            @for (n of w.names; track n.organization) {
              <span class="name" [class.fresh]="fresh().includes(n.organization)">{{ n.organization }} <b>{{ num(n.doses) }}</b></span>
            }
          </div>
        }
      }
      <div class="scan">
        <img src="/m/partners/qr-ses26.svg" width="132" height="132" alt="QR" />
        <span>{{ 'wall.scan' | t }}<br /><b dir="ltr">drwsdose.com/partners</b></span>
      </div>
    </section>
  `,
  styles: `
    :host { display: block; }
    .wall { min-height: calc(100dvh - 140px); display: grid; align-content: center; justify-items: center; gap: 18px; text-align: center; padding: 24px 0; }
    h1 { margin: 0; color: var(--dose-navy); font-size: clamp(1.4rem, 5vw, 2.4rem); }
    .count { display: grid; justify-items: center; line-height: 1; }
    .count strong { font-size: clamp(4.5rem, 22vw, 11rem); color: var(--dose-primary); font-variant-numeric: tabular-nums; }
    .count span { font-size: clamp(1.2rem, 4vw, 2rem); font-weight: 800; color: var(--dose-navy); }
    .sub { margin: 0; color: var(--dose-text-muted); font-size: clamp(1rem, 3vw, 1.3rem); max-width: 34ch; }
    .names { display: flex; flex-wrap: wrap; gap: 8px; justify-content: center; max-width: 900px; align-items: center; }
    .thanks { font-weight: 800; color: var(--dose-navy); }
    .name { background: var(--dose-surface); border: 1.5px solid var(--dose-border); border-radius: 999px; padding: 6px 14px; font-weight: 700; }
    .name b { color: var(--dose-success); }
    .name.fresh { border-color: var(--dose-success); background: var(--dose-success-50); animation: pop .6s ease; }
    @keyframes pop { 40% { transform: scale(1.12); } }
    .scan { display: flex; gap: 14px; align-items: center; background: var(--dose-surface); border-radius: var(--dose-radius); padding: 12px 16px; box-shadow: var(--dose-shadow); text-align: start; font-weight: 700; }
    .scan img { background: #fff; border-radius: 8px; }
  `,
})
export class PartnersWall implements OnDestroy {
  private readonly api = inject(Api);
  private readonly event = inject(ActivatedRoute).snapshot.queryParamMap.get('event');
  protected readonly data = signal<PledgeWall | null>(null);
  protected readonly shown = signal(0);
  protected readonly fresh = signal<string[]>([]);
  private timer = setInterval(() => this.load(), 10_000);
  private anim = 0;

  constructor() { this.load(); }
  ngOnDestroy() { clearInterval(this.timer); cancelAnimationFrame(this.anim); }

  protected num(n: number) { return n.toLocaleString(document.documentElement.lang === 'en' ? 'en' : 'ar-EG'); }

  private async load() {
    try {
      const w = await this.api.pledgeWall(this.event);
      const before = this.data();
      if (before) {
        const old = new Map(before.names.map((n) => [n.organization, n.doses]));
        this.fresh.set(w.names.filter((n) => old.get(n.organization) !== n.doses).map((n) => n.organization));
      }
      this.data.set(w);
      this.countTo(w.totalDoses);
    } catch { /* keep the last number on screen */ }
  }

  private countTo(target: number) {
    cancelAnimationFrame(this.anim);
    const from = this.shown();
    if (from === target) return;
    const start = performance.now();
    const step = (t: number) => {
      const p = Math.min(1, (t - start) / 1400);
      this.shown.set(Math.round(from + (target - from) * (1 - Math.pow(1 - p, 3))));
      if (p < 1) this.anim = requestAnimationFrame(step);
    };
    this.anim = requestAnimationFrame(step);
  }
}
