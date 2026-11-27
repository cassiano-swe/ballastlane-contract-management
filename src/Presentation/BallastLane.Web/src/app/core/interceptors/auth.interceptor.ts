import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { tap } from 'rxjs';
import { AuthService } from '../authentication/auth.service';
import { AuthState } from '../authentication/auth.state';
import { ApiConfig } from '../config/api-config';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!inject(ApiConfig).isProtectedBusinessUrl(request.url) ||
      request.headers.has('Authorization')) return next(request);

  const session = inject(AuthState).current();
  if (!session) return next(request);

  const auth = inject(AuthService);
  return next(request.clone({ setHeaders: { Authorization: `Bearer ${session.token}` } })).pipe(
    tap({ error: (error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) auth.logout(session.id);
    } }),
  );
};
