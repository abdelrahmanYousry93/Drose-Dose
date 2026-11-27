import { Component, ElementRef, OnDestroy, computed, effect, inject, input, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  ConnectionState, DisconnectReason, Participant, RemoteTrack, Room, RoomEvent, Track, VideoTrack,
} from 'livekit-client';
import { Api, ApiError, RoomAccess } from '../../core/api';
import { errorMessage } from '../../core/format';
import { TranslatePipe, translate } from '../../core/i18n';

/** One camera tile: attaches the LiveKit track to its own video element and detaches when it goes. */
@Component({
  selector: 'dose-tile',
  template: `<video #v autoplay playsinline [muted]="true"></video><span class="tn">{{ name() }}</span>`,
  styles: [`:host{position:relative;display:block;flex:none;width:168px;aspect-ratio:4/3;border-radius:12px;overflow:hidden;background:#121d45;border:2px solid #23306a}
    :host(.host){border-color:#FFC30A} video{width:100%;height:100%;object-fit:cover;display:block}
    .tn{position:absolute;bottom:4px;inset-inline-start:6px;font-size:.75rem;background:rgba(0,0,0,.55);padding:1px 8px;border-radius:99px;max-width:90%;overflow:hidden;white-space:nowrap;text-overflow:ellipsis}
    @media (max-width:800px){:host{width:112px}}`],
  host: { '[class.host]': 'host()' },
})
export class VideoTile {
  readonly track = input.required<VideoTrack>();
  readonly name = input('');
  readonly host = input(false);
  private readonly v = viewChild.required<ElementRef<HTMLVideoElement>>('v');
  constructor() {
    effect((onCleanup) => {
      const t = this.track(); const el = this.v().nativeElement;
      t.attach(el);
      onCleanup(() => t.detach(el));
    });
  }
}

interface Tile { key: string; name: string; host: boolean; track: VideoTrack }
interface Person { identity: string; name: string; host: boolean; me: boolean; micOn: boolean; micSid: string | null; hand: boolean; speaking: boolean }
interface ChatLine { from: string; text: string; host: boolean; at: number }

/**
 * Dose's own video room (LiveKit). The teacher's screen share (or camera) fills the stage; students join with their
 * microphone only, can raise a hand and chat. The host (teacher / admin) can share screen and camera, mute a student
 * and remove someone. One participant per booking: opening the same link on a second device moves the seat there.
 */
@Component({
  selector: 'dose-room',
  imports: [FormsModule, RouterLink, TranslatePipe, VideoTile],
  template: `
  <div class="room" [class.live]="state() === 'live'">
    @switch (state()) {
      @case ('loading') { <div class="center"><div class="spin"></div></div> }
      @case ('error') {
        <div class="center card">
          <h2>{{ 'room.cantEnter' | t }}</h2><p>{{ error() }}</p>
          @if (token()) { <a class="dose-btn" [routerLink]="['/b', token()]">{{ 'room.backToBooking' | t }}</a> }
        </div>
      }
      @case ('lobby') {
        <div class="center card">
          <div class="pill">💊 {{ 'room.badge' | t }}</div>
          <h2>{{ access()?.title }}</h2>
          @if (access()?.teacherName) { <p class="muted">{{ 'room.with' | t }} {{ access()?.teacherName }}</p> }
          <p>{{ (isHost() ? 'room.hostHint' : 'room.studentHint') | t }}</p>
          <button class="dose-btn dose-btn--primary big" (click)="enter()" [disabled]="busy()">{{ 'room.enter' | t }}</button>
        </div>
      }
      @case ('ended') {
        <div class="center card"><h2>{{ endTitle() | t }}</h2><p>{{ endText() | t }}</p>
          @if (endReason() !== 'ended') { <button class="dose-btn dose-btn--primary" (click)="enter()">{{ 'room.rejoin' | t }}</button> }
          @if (token()) { <a class="dose-btn" [routerLink]="['/b', token()]">{{ 'room.backToBooking' | t }}</a> }
        </div>
      }
      @case ('live') {
        <header class="bar">
          <div class="ttl"><b>{{ access()?.title }}</b>@if (access()?.teacherName) {<span> · {{ access()?.teacherName }}</span>}</div>
          <span class="count">👥 {{ people().length }}</span>
          @if (reconnecting()) { <span class="warn">{{ 'room.reconnecting' | t }}</span> }
        </header>
        <main class="grid">
          <section class="stage" #stageBox (dblclick)="toggleFull()">
            <video #stage autoplay playsinline [class.hidden]="!hasStage()"></video>
            @if (hasStage()) {
              <button class="fs" (click)="toggleFull(); $event.stopPropagation()" [attr.aria-label]="(isFull() ? 'room.exitFull' : 'room.full') | t">{{ isFull() ? '✕' : '⛶' }}<small>{{ (isFull() ? 'room.exitFull' : 'room.full') | t }}</small></button>
            }
            @if (!hasStage()) {
              <div class="empty">{{ (isHost() ? 'room.hostEmpty' : hostHere() ? 'room.teacherNoVideo' : 'room.waitTeacher') | t }}</div>
            }
          </section>
          @if (tiles().length) {
            <div class="strip">
              @for (v of tiles(); track v.key) { <dose-tile [track]="v.track" [name]="v.name" [host]="v.host" /> }
            </div>
          }
          <aside class="side">
            <nav class="tabs">
              <button [class.on]="tab() === 'people'" (click)="tab.set('people')">{{ 'room.people' | t }} ({{ people().length }})</button>
              <button [class.on]="tab() === 'chat'" (click)="tab.set('chat'); unread.set(0)">{{ 'room.chat' | t }} @if (unread()) {<i>{{ unread() }}</i>}</button>
            </nav>
            @if (tab() === 'people') {
              <ul class="people">
                @for (p of people(); track p.identity) {
                  <li [class.speak]="p.speaking">
                    <span class="nm">@if (p.hand) {✋ }{{ p.name }}@if (p.me) { ({{ 'room.you' | t }})}@if (p.host) { · {{ 'room.teacher' | t }}}</span>
                    <span class="mic">{{ p.micOn ? '🎙️' : '🔇' }}</span>
                    @if (isHost() && !p.host && !p.me) {
                      @if (p.micOn && p.micSid) { <button class="mini" (click)="mute(p)">{{ 'room.mute' | t }}</button> }
                      @if (p.hand) { <button class="mini" (click)="lowerHand(p)">{{ 'room.lowerHand' | t }}</button> }
                      <button class="mini danger" (click)="remove(p)">{{ 'room.remove' | t }}</button>
                    }
                  </li>
                }
              </ul>
            } @else {
              <div class="chat">
                <div class="lines" #lines>
                  @for (c of chat(); track c.at) { <p [class.host]="c.host"><b>{{ c.from }}:</b> {{ c.text }}</p> }
                  @empty { <p class="muted">{{ 'room.chatEmpty' | t }}</p> }
                </div>
                <form (ngSubmit)="send()"><input name="msg" [(ngModel)]="draft" [placeholder]="'room.chatPh' | t" maxlength="300" autocomplete="off" /><button class="dose-btn dose-btn--primary">{{ 'room.send' | t }}</button></form>
              </div>
            }
          </aside>
        </main>
        <footer class="controls">
          <button class="ctl" [class.on]="micOn()" (click)="toggleMic()">{{ micOn() ? '🎙️' : '🔇' }}<small>{{ (micOn() ? 'room.micOn' : 'room.micOff') | t }}</small></button>
          <button class="ctl" [class.on]="camOn()" (click)="toggleCam()">📷<small>{{ (camOn() ? 'room.camOn' : 'room.camOff') | t }}</small></button>
          @if (isHost()) {
            <button class="ctl" [class.on]="screenOn()" (click)="toggleScreen()">🖥️<small>{{ (screenOn() ? 'room.stopShare' : 'room.share') | t }}</small></button>
          } @else {
            <button class="ctl" [class.on]="handUp()" (click)="toggleHand()">✋<small>{{ (handUp() ? 'room.handDown' : 'room.handUp') | t }}</small></button>
          }
          <button class="ctl leave" (click)="leave()">🚪<small>{{ 'room.leave' | t }}</small></button>
        </footer>
        @if (notice()) { <div class="toast">{{ notice() }}</div> }
      }
    }
    <div #audio class="hidden"></div>
  </div>`,
  styles: [`
    :host{display:block}
    .room{min-height:100dvh;background:#0b1430;color:#eef2ff;display:flex;flex-direction:column}
    .center{flex:1;display:grid;place-items:center;text-align:center;padding:24px}
    .card{max-width:460px;margin:auto;background:#121d45;border:1px solid #23306a;border-radius:20px;padding:28px 22px;display:grid;gap:12px;justify-items:center}
    .card h2{margin:0;font-size:1.3rem} .muted{color:#9fb0e0} .pill{background:#FFC30A;color:#0B1F6B;font-weight:800;border-radius:99px;padding:2px 14px}
    .big{font-size:1.1rem;padding:12px 32px}
    .spin{width:42px;height:42px;border-radius:50%;border:4px solid #23306a;border-top-color:#FFC30A;animation:sp 1s linear infinite} @keyframes sp{to{transform:rotate(360deg)}}
    .bar{display:flex;align-items:center;gap:12px;padding:10px 14px;border-bottom:1px solid #1d2856}
    .ttl{flex:1;min-width:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis} .ttl span{color:#9fb0e0}
    .warn{color:#FFC30A;font-weight:700}
    .grid{flex:1;display:grid;grid-template-columns:1fr 320px;grid-template-rows:1fr auto;min-height:0}
    .stage{grid-column:1;grid-row:1}.side{grid-column:2;grid-row:1/span 2}
    .strip{grid-column:1;grid-row:2;display:flex;gap:8px;overflow-x:auto;padding:8px;background:#070d24}
    .stage{position:relative;background:#000;display:grid;place-items:center;min-height:240px}
    .stage video{width:100%;height:100%;max-height:calc(100dvh - 290px);object-fit:contain}
    .empty{color:#9fb0e0;padding:20px;text-align:center}
    .fs{position:absolute;top:10px;inset-inline-end:10px;display:flex;align-items:center;gap:6px;background:rgba(0,0,0,.6);color:#fff;border:1px solid rgba(255,255,255,.3);border-radius:10px;padding:6px 10px;font-size:1.1rem;cursor:pointer;z-index:2}
    .fs small{font-size:.75rem}
    .stage:fullscreen{width:100vw;height:100vh;display:block} .stage:fullscreen video{position:absolute;inset:0;width:100%;height:100%;max-height:none;object-fit:contain}
    .stage:-webkit-full-screen{width:100vw;height:100vh;display:block} .stage:-webkit-full-screen video{position:absolute;inset:0;width:100%;height:100%;max-height:none;object-fit:contain}
    .side{border-inline-start:1px solid #1d2856;display:flex;flex-direction:column;min-height:0}
    .tabs{display:flex} .tabs button{flex:1;background:none;border:0;color:#9fb0e0;padding:10px;font:inherit;font-weight:700;border-bottom:2px solid transparent;cursor:pointer}
    .tabs button.on{color:#fff;border-color:#FFC30A} .tabs i{font-style:normal;background:#e5484d;color:#fff;border-radius:99px;padding:0 7px;margin-inline-start:4px}
    .people{list-style:none;margin:0;padding:8px;overflow:auto;display:grid;gap:6px;align-content:start}
    .people li{display:flex;align-items:center;gap:6px;flex-wrap:wrap;background:#121d45;border-radius:10px;padding:8px 10px;border:1px solid transparent}
    .people li.speak{border-color:#2fb36b} .nm{flex:1;min-width:0}
    .mini{background:#23306a;color:#fff;border:0;border-radius:8px;padding:3px 9px;font:inherit;font-size:.8rem;cursor:pointer} .mini.danger{background:#7a1f2a}
    .chat{flex:1;display:flex;flex-direction:column;min-height:0}
    .lines{flex:1;overflow:auto;padding:8px 12px;min-height:160px} .lines p{margin:4px 0;word-break:break-word} .lines p.host b{color:#FFC30A}
    .chat form{display:flex;gap:6px;padding:8px;border-top:1px solid #1d2856}
    .chat input{flex:1;min-width:0;border-radius:10px;border:1px solid #23306a;background:#0b1430;color:#fff;padding:8px 10px;font:inherit}
    .controls{display:flex;justify-content:center;gap:10px;padding:10px;border-top:1px solid #1d2856;flex-wrap:wrap}
    .ctl{display:grid;justify-items:center;gap:2px;min-width:74px;background:#121d45;color:#fff;border:1px solid #23306a;border-radius:14px;padding:8px 10px;font-size:1.3rem;cursor:pointer}
    .ctl small{font-size:.72rem;color:#c9d3f5} .ctl.on{border-color:#FFC30A;background:#1d2a62} .ctl.leave{background:#7a1f2a;border-color:#7a1f2a}
    .toast{position:fixed;bottom:90px;inset-inline:0;margin:auto;width:max-content;max-width:90vw;background:#FFC30A;color:#0B1F6B;font-weight:700;border-radius:12px;padding:8px 16px}
    .hidden{display:none!important}
    @media (max-width:800px){.grid{grid-template-columns:1fr;grid-template-rows:auto auto 1fr}.stage{grid-row:1}.strip{grid-row:2}.side{grid-column:1;grid-row:3;border-inline-start:0;border-top:1px solid #1d2856;max-height:45dvh}.stage video{max-height:40dvh}}
  `],
})
export class RoomPage implements OnDestroy {
  private readonly api = inject(Api);
  private readonly router = inject(Router);
  readonly token = input<string>();
  readonly sessionId = input<string>();

  protected readonly state = signal<'loading' | 'lobby' | 'live' | 'ended' | 'error'>('loading');
  protected readonly access = signal<RoomAccess | null>(null);
  protected readonly error = signal('');
  protected readonly busy = signal(false);
  protected readonly people = signal<Person[]>([]);
  protected readonly tiles = signal<Tile[]>([]);
  protected readonly chat = signal<ChatLine[]>([]);
  protected readonly unread = signal(0);
  protected readonly tab = signal<'people' | 'chat'>('people');
  protected readonly micOn = signal(false);
  protected readonly camOn = signal(false);
  protected readonly screenOn = signal(false);
  protected readonly handUp = signal(false);
  protected readonly hasStage = signal(false);
  protected readonly reconnecting = signal(false);
  protected readonly notice = signal('');
  protected readonly endReason = signal<'left' | 'ended' | 'moved' | 'removed' | 'lost'>('left');
  protected readonly isHost = computed(() => this.access()?.role === 'host');
  protected readonly hostHere = computed(() => this.people().some((p) => p.host && !p.me));
  protected readonly endTitle = computed(() => `room.end.${this.endReason()}.title`);
  protected readonly endText = computed(() => `room.end.${this.endReason()}.text`);
  protected draft = '';

  private readonly stageEl = viewChild<ElementRef<HTMLVideoElement>>('stage');
  private readonly stageBox = viewChild<ElementRef<HTMLElement>>('stageBox');
  protected readonly isFull = signal(false);
  private readonly onFullChange = () => this.isFull.set(!!document.fullscreenElement);
  private readonly audioEl = viewChild<ElementRef<HTMLDivElement>>('audio');
  private room: Room | null = null;
  private stageTrack: VideoTrack | null = null;
  private hands = new Set<string>();
  private noticeTimer: ReturnType<typeof setTimeout> | undefined;
  private readonly enc = new TextEncoder();
  private readonly dec = new TextDecoder();

  constructor() {
    queueMicrotask(() => this.load());
    document.addEventListener('fullscreenchange', this.onFullChange);
  }

  /** Full screen for the stage (the shared screen reads much better); iPhone only allows it on the video itself. */
  protected async toggleFull() {
    if (document.fullscreenElement) { await document.exitFullscreen().catch(() => undefined); return; }
    const box = this.stageBox()?.nativeElement;
    const video = this.stageEl()?.nativeElement as (HTMLVideoElement & { webkitEnterFullscreen?: () => void }) | undefined;
    if (box?.requestFullscreen) {
      await box.requestFullscreen().catch(() => video?.webkitEnterFullscreen?.());
      // On phones, turn sideways so the shared screen uses the whole width.
      await (screen.orientation as ScreenOrientation & { lock?: (o: string) => Promise<void> })?.lock?.('landscape').catch(() => undefined);
    } else video?.webkitEnterFullscreen?.();
  }

  private async load() {
    try {
      const a = this.token() ? await this.api.roomPass(this.token()!) : await this.api.roomHostPass(this.sessionId()!);
      this.access.set(a);
      this.state.set('lobby');
    } catch (e) {
      if (e instanceof ApiError && e.status === 401 && this.sessionId()) {
        await this.router.navigate(['/login'], { queryParams: { next: `/room/host/${this.sessionId()}` } });
        return;
      }
      this.error.set(errorMessage(e));
      this.state.set('error');
    }
  }

  protected async enter() {
    this.busy.set(true);
    try {
      // A fresh pass each time (the first may be old by now).
      const a = this.token() ? await this.api.roomPass(this.token()!) : await this.api.roomHostPass(this.sessionId()!);
      this.access.set(a);
      const room = new Room({ adaptiveStream: { pixelDensity: 'screen' }, dynacast: true });
      this.room = room;
      room
        .on(RoomEvent.ParticipantConnected, () => this.refresh())
        .on(RoomEvent.ParticipantDisconnected, (p) => { this.hands.delete(p.identity); this.refresh(); })
        .on(RoomEvent.TrackSubscribed, (t: RemoteTrack) => { if (t.kind === Track.Kind.Audio) this.audioEl()?.nativeElement.appendChild(t.attach()); this.refresh(); })
        .on(RoomEvent.TrackUnsubscribed, (t: RemoteTrack) => { t.detach().forEach((el) => el.remove()); this.refresh(); })
        .on(RoomEvent.TrackMuted, () => this.refresh())
        .on(RoomEvent.TrackUnmuted, () => this.refresh())
        .on(RoomEvent.LocalTrackPublished, () => this.refresh())
        .on(RoomEvent.LocalTrackUnpublished, () => this.refresh())
        .on(RoomEvent.ActiveSpeakersChanged, () => this.refresh())
        .on(RoomEvent.DataReceived, (payload, p, _k, topic) => this.onData(payload, p, topic))
        .on(RoomEvent.ConnectionStateChanged, (s) => this.reconnecting.set(s === ConnectionState.Reconnecting))
        .on(RoomEvent.Disconnected, (reason) => this.onDisconnected(reason));
      await room.connect(a.serverUrl, a.token);
      this.state.set('live');
      await room.startAudio().catch(() => undefined);
      this.refresh();
    } catch (e) {
      this.error.set(e instanceof ApiError ? errorMessage(e) : translate('room.connectFailed'));
      this.state.set('error');
    } finally {
      this.busy.set(false);
    }
  }

  private onDisconnected(reason?: DisconnectReason) {
    this.detachStage();
    this.tiles.set([]);
    this.room = null;
    this.micOn.set(false); this.camOn.set(false); this.screenOn.set(false); this.handUp.set(false);
    if (this.state() !== 'live') return;
    this.endReason.set(
      reason === DisconnectReason.DUPLICATE_IDENTITY ? 'moved'
      : reason === DisconnectReason.PARTICIPANT_REMOVED ? 'removed'
      : reason === DisconnectReason.ROOM_DELETED ? 'ended'
      : reason === DisconnectReason.CLIENT_INITIATED ? 'left' : 'lost');
    this.state.set('ended');
  }

  /** Rebuild the people list and choose what fills the stage: a host's screen share, else a host's camera. */
  private refresh() {
    const room = this.room;
    if (!room) return;
    const all: Participant[] = [room.localParticipant, ...room.remoteParticipants.values()];
    const speaking = new Set(room.activeSpeakers.map((p) => p.identity));
    const list = all.map((p): Person => {
      const mic = p.getTrackPublication(Track.Source.Microphone);
      return {
        identity: p.identity, name: p.name || p.identity, host: this.roleOf(p) === 'host', me: p === room.localParticipant,
        micOn: !!mic && !mic.isMuted, micSid: mic?.trackSid ?? null, hand: this.hands.has(p.identity), speaking: speaking.has(p.identity),
      };
    });
    list.sort((a, b) => Number(b.host) - Number(a.host) || Number(b.hand) - Number(a.hand) || a.name.localeCompare(b.name, 'ar'));
    this.people.set(list);

    let best: VideoTrack | null = null;
    for (const p of all) {
      if (this.roleOf(p) !== 'host') continue;
      const screen = p.getTrackPublication(Track.Source.ScreenShare);
      const cam = p.getTrackPublication(Track.Source.Camera);
      // The teacher sees their own camera big too (e.g. writing on paper), not only the students.
      const pick = (screen?.videoTrack && !screen.isMuted ? screen.videoTrack : null) ?? (cam?.videoTrack && !cam.isMuted ? cam.videoTrack : null);
      if (pick && (!best || p.getTrackPublication(Track.Source.ScreenShare)?.videoTrack === pick)) best = pick;
    }
    if (best !== this.stageTrack) {
      this.detachStage();
      const el = this.stageEl()?.nativeElement;
      if (best && el) { best.attach(el); this.stageTrack = best; }
    }
    this.hasStage.set(!!this.stageTrack);

    // Cameras always stay visible (teacher and students see each other), except the one already on the stage.
    const tiles: Tile[] = [];
    for (const p of all) {
      const cam = p.getTrackPublication(Track.Source.Camera);
      const t = cam?.videoTrack;
      if (!t || cam.isMuted || t === this.stageTrack) continue;
      const me = p === room.localParticipant;
      tiles.push({ key: `${p.identity}:${t.sid ?? 'local'}`, name: me ? translate('room.you') : (p.name || p.identity), host: this.roleOf(p) === 'host', track: t });
    }
    tiles.sort((a, b) => Number(b.host) - Number(a.host));
    const prev = this.tiles();
    if (prev.length !== tiles.length || prev.some((x, i) => x.key !== tiles[i].key || x.track !== tiles[i].track)) this.tiles.set(tiles);
  }

  private detachStage() { if (this.stageTrack) { this.stageTrack.detach(); this.stageTrack = null; } }

  private roleOf(p: Participant): string {
    try { return JSON.parse(p.metadata || '{}').role ?? 'student'; } catch { return 'student'; }
  }

  private onData(payload: Uint8Array, p: Participant | undefined, topic?: string) {
    let msg: { text?: string; up?: boolean; identity?: string; mute?: boolean };
    try { msg = JSON.parse(this.dec.decode(payload)); } catch { return; }
    if (topic === 'chat' && p && msg.text) {
      this.chat.update((c) => [...c.slice(-199), { from: p.name || p.identity, text: String(msg.text).slice(0, 300), host: this.roleOf(p) === 'host', at: Date.now() + Math.random() }]);
      if (this.tab() !== 'chat') this.unread.update((n) => n + 1);
    } else if (topic === 'hand' && p) {
      // A host may lower someone else's hand; everyone else only their own.
      const who = this.roleOf(p) === 'host' && msg.identity ? msg.identity : p.identity;
      if (msg.up) this.hands.add(who); else this.hands.delete(who);
      if (who === this.room?.localParticipant.identity) this.handUp.set(!!msg.up);
      if (msg.up && this.isHost()) this.flash(`✋ ${p.name}`);
      this.refresh();
    }
  }

  private async publish(topic: string, body: unknown) {
    await this.room?.localParticipant.publishData(this.enc.encode(JSON.stringify(body)), { reliable: true, topic });
  }

  protected async send() {
    const text = this.draft.trim();
    if (!text || !this.room) return;
    this.draft = '';
    await this.publish('chat', { text });
    const me = this.room.localParticipant;
    this.chat.update((c) => [...c.slice(-199), { from: me.name || translate('room.you'), text, host: this.isHost(), at: Date.now() + Math.random() }]);
  }

  protected async toggleMic() {
    if (!this.room) return;
    try {
      const on = !this.micOn();
      await this.room.localParticipant.setMicrophoneEnabled(on);
      this.micOn.set(on);
    } catch { this.flash(translate('room.micBlocked')); }
    this.refresh();
  }

  protected async toggleCam() {
    if (!this.room) return;
    try {
      const on = !this.camOn();
      await this.room.localParticipant.setCameraEnabled(on);
      this.camOn.set(on);
    } catch { this.flash(translate('room.camBlocked')); }
    this.refresh();
  }

  protected async toggleScreen() {
    if (!this.room) return;
    try {
      const on = !this.screenOn();
      // Sharp text: capture as text content at 1080p and give the share enough bitrate.
      await this.room.localParticipant.setScreenShareEnabled(on, { audio: true, contentHint: 'text', resolution: { width: 1920, height: 1080, frameRate: 15 } },
        { screenShareEncoding: { maxBitrate: 3_000_000, maxFramerate: 15 }, screenShareSimulcastLayers: [] });
      this.screenOn.set(on);
    } catch { this.flash(translate('room.shareFailed')); }
    this.refresh();
  }

  protected async toggleHand() {
    const up = !this.handUp();
    this.handUp.set(up);
    const me = this.room?.localParticipant.identity;
    if (me) { if (up) this.hands.add(me); else this.hands.delete(me); }
    await this.publish('hand', { up });
    this.refresh();
  }

  protected async lowerHand(p: Person) {
    this.hands.delete(p.identity);
    await this.publish('hand', { up: false, identity: p.identity });
    this.refresh();
  }

  protected async mute(p: Person) {
    if (!p.micSid) return;
    try { await this.api.roomModerate(this.sessionIdForHost(), { action: 'mute', identity: p.identity, trackSid: p.micSid }); }
    catch (e) { this.flash(errorMessage(e)); }
  }

  protected async remove(p: Person) {
    if (!confirm(translate('room.confirmRemove').replace('{name}', p.name))) return;
    try { await this.api.roomModerate(this.sessionIdForHost(), { action: 'remove', identity: p.identity }); }
    catch (e) { this.flash(errorMessage(e)); }
  }

  private sessionIdForHost() { return this.sessionId()!; }

  protected async leave() {
    await this.room?.disconnect();
  }

  private flash(text: string) {
    this.notice.set(text);
    clearTimeout(this.noticeTimer);
    this.noticeTimer = setTimeout(() => this.notice.set(''), 3500);
  }

  ngOnDestroy() {
    document.removeEventListener('fullscreenchange', this.onFullChange);
    clearTimeout(this.noticeTimer);
    void this.room?.disconnect();
  }
}
