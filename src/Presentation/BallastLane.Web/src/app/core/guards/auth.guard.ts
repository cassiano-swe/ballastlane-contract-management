import { inject } from '@angular/core';
import { CanActivateChildFn, CanActivateFn, RedirectFunction, Router } from '@angular/router';
import { AuthState } from '../authentication/auth.state';

export const requireSession: CanActivateFn & CanActivateChildFn = () =>
  inject(AuthState).current() !== null || inject(Router).createUrlTree(['/login']);

export const anonymousOnly: CanActivateFn = () =>
  inject(AuthState).current() === null || inject(Router).createUrlTree(['/dashboard']);

export const sessionDestination: RedirectFunction = () =>
  inject(AuthState).current() ? '/dashboard' : '/login';
