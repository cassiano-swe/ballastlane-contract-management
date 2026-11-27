import { Routes } from '@angular/router';
import { anonymousOnly, requireSession, sessionDestination } from './core/guards/auth.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: sessionDestination },
  {
    path: 'login',
    canActivate: [anonymousOnly],
    loadComponent: () => import('./layouts/auth-layout/auth-layout').then((m) => m.AuthLayout),
    children: [{
      path: '',
      loadComponent: () => import('./features/authentication/login/login').then((m) => m.Login),
    }],
  },
  {
    path: '',
    canActivate: [requireSession],
    canActivateChild: [requireSession],
    loadComponent: () => import('./layouts/main-layout/main-layout').then((m) => m.MainLayout),
    children: [{
      path: 'dashboard',
      loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
    }],
  },
  { path: '**', redirectTo: sessionDestination },
];
