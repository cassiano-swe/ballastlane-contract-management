import { Injectable, NgZone, computed, inject, signal } from '@angular/core';

interface Session {
  id: number;
  token: string;
  expiresAt: number;
}

@Injectable({ providedIn: 'root' })
export class AuthState {
  private readonly zone = inject(NgZone);
  private readonly session = signal<Session | null>(null);
  private timer?: ReturnType<typeof setTimeout>;
  private nextId = 0;

  readonly authenticated = computed(() => this.session() !== null);

  current(): Readonly<Session> | null {
    const session = this.session();
    return session && session.expiresAt > Date.now() ? session : null;
  }

  start(token: string, expiresAt: number, onExpiry: () => void): void {
    this.clear();
    const session = { id: ++this.nextId, token, expiresAt };
    this.session.set(session);
    const schedule = () => {
      if (this.session()?.id !== session.id) return;
      const remaining = expiresAt - Date.now();
      if (remaining <= 0) {
        this.zone.run(() => {
          this.clear(session.id);
          onExpiry();
        });
        return;
      }
      this.timer = setTimeout(schedule, Math.min(remaining, 2_147_483_647));
    };
    this.zone.runOutsideAngular(schedule);
  }

  clear(id?: number): boolean {
    if (id !== undefined && this.session()?.id !== id) return false;
    clearTimeout(this.timer);
    this.timer = undefined;
    this.session.set(null);
    return true;
  }
}
