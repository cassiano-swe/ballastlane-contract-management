import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/authentication/auth.service';
import { LoginResult } from '../../../core/authentication/auth.models';

@Component({
  imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private retryTimer?: ReturnType<typeof setTimeout>;
  private retryAt = 0;

  readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    password: ['', [Validators.required, Validators.maxLength(1024)]],
  });
  readonly pending = signal(false);
  readonly showPassword = signal(false);
  readonly throttled = signal(false);
  readonly feedback = signal('');
  readonly busy = computed(() => this.pending() || this.throttled());

  constructor() {
    this.destroyRef.onDestroy(() => clearTimeout(this.retryTimer));
  }

  togglePassword(): void {
    this.showPassword.update((visible) => !visible);
  }

  submit(): void {
    if (this.pending() || Date.now() < this.retryAt) return;
    this.feedback.set('');
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.pending.set(true);
    this.auth.login(this.form.getRawValue()).pipe(
      takeUntilDestroyed(this.destroyRef),
      finalize(() => this.pending.set(false)),
    ).subscribe((result) => this.handleResult(result));
  }

  private handleResult(result: LoginResult): void {
    switch (result.kind) {
      case 'success':
        this.form.controls.password.reset();
        void this.router.navigateByUrl('/dashboard', { replaceUrl: true });
        break;
      case 'credentials': this.feedback.set('Invalid email or password.'); break;
      case 'validation': this.feedback.set('Check your email and password and try again.'); break;
      case 'network': this.feedback.set('Could not reach the sign-in service. Check your connection and try again.'); break;
      case 'unexpected': this.feedback.set('Sign-in is unavailable right now. Try again later.'); break;
      case 'rate-limited':
        this.feedback.set('Too many sign-in attempts. Try again later.');
        if (result.retryAt !== null) {
          this.retryAt = result.retryAt;
          this.resumeWhenReady();
        }
        break;
    }
  }

  private resumeWhenReady(): void {
    clearTimeout(this.retryTimer);
    const remaining = this.retryAt - Date.now();
    this.throttled.set(remaining > 0);
    if (remaining > 0) {
      this.retryTimer = setTimeout(() => this.resumeWhenReady(), Math.min(remaining, 2_147_483_647));
    }
  }
}
