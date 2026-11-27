import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, defer, map, of } from 'rxjs';
import { ApiConfig } from '../config/api-config';
import { LoginRequest, LoginResponse, LoginResult, ProblemDetails } from './auth.models';
import { AuthState } from './auth.state';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ApiConfig);
  private readonly state = inject(AuthState);
  private readonly router = inject(Router);
  private attempt = 0;

  login(request: LoginRequest): Observable<LoginResult> {
    return defer(() => {
      const attempt = ++this.attempt;
      return this.http.post<Partial<LoginResponse> | null>(this.config.loginUrl, request).pipe(
        map((response): LoginResult => {
          if (attempt !== this.attempt) return { kind: 'cancelled' };
          const expiresAt = expiration(response);
          if (expiresAt === null || typeof response?.accessToken !== 'string') {
            return { kind: 'unexpected' };
          }
          this.state.start(response.accessToken, expiresAt, () => this.returnToLogin());
          return { kind: 'success' };
        }),
        catchError((error: unknown) => of(
          attempt === this.attempt ? loginFailure(error) : { kind: 'cancelled' } as const,
        )),
      );
    });
  }

  logout(sessionId?: number): void {
    if (this.state.clear(sessionId)) this.returnToLogin();
  }

  private returnToLogin(): void {
    this.attempt++;
    void this.router.navigateByUrl('/login', { replaceUrl: true });
  }
}

function expiration(response: Partial<LoginResponse> | null): number | null {
  const token = response?.accessToken;
  const lifetime = response?.expiresIn;
  if (response?.tokenType !== 'Bearer' || typeof token !== 'string' ||
      typeof lifetime !== 'number' || !Number.isSafeInteger(lifetime) || lifetime <= 0 ||
      !/^[\w-]+\.[\w-]+\.[\w-]+$/.test(token)) return null;

  try {
    // The business API validates the signature. Only the expiry is read here.
    const [header, payload, signature] = token.split('.');
    const metadata: { alg?: unknown } | null = parseJwtPart<{ alg?: unknown } | null>(header);
    if (typeof metadata?.alg !== 'string' || !metadata.alg || metadata.alg === 'none' ||
        signature.length % 4 === 1) return null;
    const claims: { exp?: unknown } | null = parseJwtPart<{ exp?: unknown } | null>(payload);
    const exp = claims?.exp;
    if (typeof exp !== 'number' || !Number.isSafeInteger(exp)) return null;
    const now = Date.now();
    const expiresAt = Math.min(now + lifetime * 1000, exp * 1000);
    return expiresAt > now ? expiresAt : null;
  } catch {
    return null;
  }
}

function parseJwtPart<T>(part: string): T {
  const bytes = atob(part.replace(/-/g, '+').replace(/_/g, '/'));
  return JSON.parse(bytes);
}

function loginFailure(error: unknown): LoginResult {
  if (!(error instanceof HttpErrorResponse)) return { kind: 'unexpected' };
  // ProblemDetails may be missing or non-JSON. Use the HTTP status, never its raw detail.
  const response: { status: number; error: ProblemDetails | null } = error;
  switch (response.status) {
    case 0: return { kind: 'network' };
    case 400: return { kind: 'validation' };
    case 401: return { kind: 'credentials' };
    case 429: return { kind: 'rate-limited', retryAt: retryAt(error.headers.get('Retry-After')) };
    default: return { kind: 'unexpected' };
  }
}

function retryAt(header: string | null): number | null {
  if (!header) return null;
  const now = Date.now();
  const time = /^\d+$/.test(header) ? now + Number(header) * 1000 : Date.parse(header);
  return Number.isSafeInteger(time) && time > now ? time : null;
}
