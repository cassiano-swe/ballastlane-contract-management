import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from './app.routes';
import { AuthState } from './core/authentication/auth.state';

describe('Application routes', () => {
  let state: AuthState;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter(routes), provideHttpClient()] });
    state = TestBed.inject(AuthState);
    router = TestBed.inject(Router);
  });

  afterEach(() => state.clear());

  it('redirects anonymous root, unknown routes and dashboard to login', async () => {
    const harness = await RouterTestingHarness.create('/');
    expect(router.url).toBe('/login');
    for (const url of ['/dashboard', '/missing']) {
      await harness.navigateByUrl(url);
      expect(router.url).toBe('/login');
    }
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('Sign in');
  });

  it('rejects a session whose expiry passed before its timer ran', async () => {
    const initialTime = Date.now();
    state.start('token', initialTime + 10000, () => {});
    spyOn(Date, 'now').and.returnValue(initialTime + 10001);
    await RouterTestingHarness.create('/dashboard');
    expect(router.url).toBe('/login');
  });

  it('renders private navigation and dashboard, then signs out', async () => {
    state.start('token', Date.now() + 60000, () => {});
    const harness = await RouterTestingHarness.create('/login');
    expect(router.url).toBe('/dashboard');
    const layout = harness.routeNativeElement!;
    expect(layout.querySelector('main h1')?.textContent).toBe('Dashboard');
    expect(layout.querySelector('nav a')?.textContent).toBe('Dashboard');
    expect(layout.querySelectorAll('nav a').length).toBe(1);
    expect(layout.querySelector('nav')?.textContent).toContain('Customers');
    Array.from(layout.querySelectorAll('button'))
      .find((button) => button.textContent?.trim() === 'Sign out')!.click();
    await harness.fixture.whenStable();
    expect(router.url).toBe('/login');
    expect(state.authenticated()).toBeFalse();
  });
});
