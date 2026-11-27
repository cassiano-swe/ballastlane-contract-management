import { DOCUMENT, Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ApiConfig {
  private readonly document = inject(DOCUMENT);
  readonly loginUrl = this.endpoint(environment.identityApiUrl, 'api/auth/login');
  readonly customersUrl = this.endpoint(environment.businessApiUrl, 'api/customers');

  isProtectedBusinessUrl(value: string): boolean {
    const customers = new URL(this.customersUrl, this.document.baseURI);
    try {
      const request = new URL(value, this.document.baseURI);
      if (request.origin !== customers.origin || request.username || request.password) return false;
      if (request.pathname === customers.pathname) return true;
      const prefix = customers.pathname + '/';
      return request.pathname.startsWith(prefix) &&
        /^[a-f\d]{8}(?:-[a-f\d]{4}){3}-[a-f\d]{12}$/i.test(request.pathname.slice(prefix.length));
    } catch {
      return false;
    }
  }

  private endpoint(base: string, path: string): string {
    return base.replace(/\/+$/, '') + '/' + path;
  }
}
