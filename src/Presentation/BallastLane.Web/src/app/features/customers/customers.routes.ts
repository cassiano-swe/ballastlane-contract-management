import { Routes } from '@angular/router';

const form = () => import('./customer-editor').then((m) => m.CustomerEditor);

export const customerRoutes: Routes = [
  { path: '', pathMatch: 'full', loadComponent: () => import('./customer-list').then((m) => m.CustomerList) },
  { path: 'new', loadComponent: form },
  { path: ':id/edit', loadComponent: form },
  { path: ':id', loadComponent: () => import('./customer-details').then((m) => m.CustomerDetails) },
];
