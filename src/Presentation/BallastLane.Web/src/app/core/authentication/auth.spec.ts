import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { ApiConfig } from '../config/api-config';
import { authInterceptor } from '../interceptors/auth.interceptor';
import { AuthService } from './auth.service';
import { AuthState } from './auth.state';

describe('Authentication', () => {
  let auth: AuthService;
  let state: AuthState;
  let http: HttpTestingController;
  let client: HttpClient;
  let navigate: jasmine.Spy;
  let config: ApiConfig;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting()],
    });
    auth = TestBed.inject(AuthService);
    state = TestBed.inject(AuthState);
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    config = TestBed.inject(ApiConfig);
    navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
  });

  afterEach(() => {
    state.clear();
    http.verify();
  });

  function login() {
    const result = jasmine.createSpy('login result');
    const credentials = { email: 'user@example.test', password: ' exact password ' };
    auth.login(credentials).subscribe(result);
    const request = http.expectOne({ method: 'POST', url: config.loginUrl });
    expect(request.request.body).toEqual(credentials);
    expect(request.request.headers.has('Authorization')).toBeFalse();
    return { request, result };
  }

  function establish(expiresIn = 10): string {
    const { request, result } = login();
    const claims = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + 60 })).replace(/=+$/, '');
    const header = btoa(JSON.stringify({ alg: 'HS256' })).replace(/=+$/, '');
    const accessToken = header + '.' + claims + '.c2ln';
    request.flush({ accessToken, tokenType: 'Bearer', expiresIn });
    expect(result).toHaveBeenCalledOnceWith({ kind: 'success' });
    return accessToken;
  }

  it('starts anonymous and expires in memory while idle', fakeAsync(() => {
    const storage = spyOn(Storage.prototype, 'setItem');
    expect(state.authenticated()).toBeFalse();
    establish(2);
    expect(state.authenticated()).toBeTrue();
    tick(2000);
    expect(state.authenticated()).toBeFalse();
    expect(navigate).toHaveBeenCalledOnceWith('/login', { replaceUrl: true });
    expect(storage).not.toHaveBeenCalled();
  }));

  it('cancels old timers and ignores a login response after logout', fakeAsync(() => {
    establish(2);
    tick(1000);
    const accessToken = establish();
    tick(1000);
    expect(state.authenticated()).toBeTrue();
    expect(navigate).not.toHaveBeenCalled();
    const stale = login();
    auth.logout();
    stale.request.flush({ accessToken, tokenType: 'Bearer', expiresIn: 10 });
    tick(10000);
    expect(stale.result).toHaveBeenCalledOnceWith({ kind: 'cancelled' });
    expect(state.authenticated()).toBeFalse();
    expect(navigate).toHaveBeenCalledTimes(1);
  }));

  it('rejects a malformed success response', () => {
    const { request, result } = login();
    request.flush({ accessToken: 'not-a-jwt', tokenType: 'Bearer', expiresIn: 60 });
    expect(result).toHaveBeenCalledOnceWith({ kind: 'unexpected' });
    expect(state.authenticated()).toBeFalse();
  });

  for (const [status, kind] of [[400, 'validation'], [401, 'credentials'], [500, 'unexpected']] as const) {
    it(`maps login HTTP ${status} to ${kind} without a global redirect`, () => {
      const { request, result } = login();
      request.flush({ detail: 'Raw server message' }, { status, statusText: 'Error' });
      expect(result).toHaveBeenCalledOnceWith({ kind });
      expect(navigate).not.toHaveBeenCalled();
    });
  }

  it('handles network errors and Retry-After on rate limit failures', () => {
    const network = login();
    network.request.error(new ProgressEvent('error'));
    expect(network.result).toHaveBeenCalledOnceWith({ kind: 'network' });
    const limited = login();
    limited.request.flush(null, { status: 429, statusText: 'Too Many Requests',
      headers: { 'Retry-After': '23' } });
    expect(limited.result).toHaveBeenCalledOnceWith({ kind: 'rate-limited', retryAt: jasmine.any(Number) });
    expect(limited.result.calls.mostRecent().args[0].retryAt).toBeGreaterThan(Date.now() + 22000);
  });

  it('scopes the bearer and preserves other headers', () => {
    const token = establish();
    const customer = config.customersUrl + '/6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17';
    client.get(customer, { headers: { 'Accept-Language': 'en' } }).subscribe();
    const match = http.expectOne(customer);
    expect(match.request.headers.get('Authorization')).toBe('Bearer ' + token);
    expect(match.request.headers.get('Accept-Language')).toBe('en');
    match.flush(null);
    for (const url of [
      config.loginUrl, config.customersUrl + '-other', '/business-api-other/api/customers',
      '/assets/logo.svg', 'https://example.invalid' + config.customersUrl,
      '/business-api/api/examples/calculate-percentage', config.customersUrl + '/logo.svg',
    ]) {
      client.get(url).subscribe();
      const request = http.expectOne(url);
      expect(request.request.headers.has('Authorization')).toBeFalse();
      request.flush(null);
    }
  });

  it('omits credentials on an expired session before its timer runs', () => {
    establish();
    const expiresAt = state.current()!.expiresAt;
    spyOn(Date, 'now').and.returnValue(expiresAt + 1);
    client.get(config.customersUrl).subscribe();
    const request = http.expectOne(config.customersUrl);
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush([]);
  });

  it('preserves errors and only logs out for the first current protected 401', () => {
    function send() {
      const error = jasmine.createSpy('request error');
      client.get(config.customersUrl).subscribe({ error });
      return { request: http.expectOne(config.customersUrl), error };
    }
    establish();
    const stale = send();
    establish();
    stale.request.flush(null, { status: 401, statusText: 'Error' });
    expect(state.authenticated()).toBeTrue();
    for (const status of [403, 409]) {
      const { request, error } = send();
      request.flush(null, { status, statusText: 'Error' });
      expect(error.calls.mostRecent().args[0].status).toBe(status);
      expect(state.authenticated()).toBeTrue();
    }
    for (const call of [send(), send()]) {
      call.request.flush(null, { status: 401, statusText: 'Error' });
      expect(call.error.calls.mostRecent().args[0].status).toBe(401);
    }
    expect(state.authenticated()).toBeFalse();
    expect(navigate).toHaveBeenCalledTimes(1);
  });
});
