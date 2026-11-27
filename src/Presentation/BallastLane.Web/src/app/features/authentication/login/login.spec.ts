import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject } from 'rxjs';
import { LoginResult } from '../../../core/authentication/auth.models';
import { AuthService } from '../../../core/authentication/auth.service';
import { Login } from './login';

describe('Login', () => {
  let result: Subject<LoginResult>;
  let signIn: jasmine.Spy;
  let navigate: jasmine.Spy;
  let fixture: ComponentFixture<Login>;
  let component: Login;
  const payload = { email: 'user@example.test', password: ' password ' };

  beforeEach(() => {
    result = new Subject();
    signIn = jasmine.createSpy('login').and.returnValue(result);
    TestBed.configureTestingModule({
      imports: [Login],
      providers: [provideRouter([]), { provide: AuthService, useValue: { login: signIn } }],
    });
    navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.resolveTo(true);
    fixture = TestBed.createComponent(Login);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('rejects missing and malformed fields', () => {
    component.submit();
    expect(component.form.controls.password.touched).toBeTrue();
    component.form.setValue({ email: 'invalid', password: 'password' });
    component.submit();
    expect(component.form.controls.email.hasError('email')).toBeTrue();
    expect(signIn).not.toHaveBeenCalled();
  });

  it('submits once, shows loading and clears the password on success', () => {
    component.form.setValue(payload);
    const form: HTMLFormElement = fixture.nativeElement.querySelector('form');
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    component.submit();
    fixture.detectChanges();
    expect(signIn).toHaveBeenCalledOnceWith(payload);
    expect(form.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBeTrue();
    expect(form.textContent).toContain('Signing in');
    result.next({ kind: 'success' });
    result.complete();
    expect(component.form.controls.password.value).toBe('');
    expect(navigate).toHaveBeenCalledOnceWith('/dashboard', { replaceUrl: true });
  });

  it('shows generic credential feedback', () => {
    component.form.setValue(payload);
    component.submit();
    result.next({ kind: 'credentials' });
    result.complete();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent)
      .toBe('Invalid email or password.');
    expect(navigate).not.toHaveBeenCalled();
  });

  it('blocks repeated attempts for the Retry-After interval', fakeAsync(() => {
    component.form.setValue(payload);
    component.submit();
    result.next({ kind: 'rate-limited', retryAt: Date.now() + 1000 });
    result.complete();
    component.submit();
    expect(signIn).toHaveBeenCalledTimes(1);
    tick(1000);
    component.submit();
    expect(signIn).toHaveBeenCalledTimes(2);
  }));
});
