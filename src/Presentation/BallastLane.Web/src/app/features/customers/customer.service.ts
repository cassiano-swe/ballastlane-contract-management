import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { throwError } from 'rxjs';
import { ApiConfig } from '../../core/config/api-config';
import { Customer, CustomerInput, CustomerUpdate, CustomerWriteResult } from './customer.models';

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly http = inject(HttpClient);
  private readonly config = inject(ApiConfig);
  private readonly url = this.config.customersUrl;

  list() {
    return this.http.get<Customer[]>(this.url);
  }

  getById(id: string) {
    const url = this.itemUrl(id);
    if (!this.config.isProtectedBusinessUrl(url)) {
      return throwError(() => new HttpErrorResponse({ status: 400 }));
    }
    return this.http.get<Customer>(url);
  }

  create(input: CustomerInput) {
    return this.http.post<CustomerWriteResult>(this.url, input, { observe: 'response' });
  }

  update(id: string, input: CustomerUpdate) {
    return this.http.put<CustomerWriteResult>(this.itemUrl(id), input, { observe: 'response' });
  }

  deactivate(id: string, version: string) {
    return this.http.delete<void>(this.itemUrl(id), {
      params: new HttpParams().set('version', version),
      observe: 'response',
    });
  }

  private itemUrl(id: string): string {
    return this.url + '/' + encodeURIComponent(id);
  }
}
