import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthState } from '../../core/authentication/auth.state';
import { ApiConfig } from '../../core/config/api-config';
import { authInterceptor } from '../../core/interceptors/auth.interceptor';
import { Customer, CustomerInput } from './customer.models';
import { CustomerService } from './customer.service';

describe('CustomerService', () => {
  const id = '6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17';
  const version = 'c53db246-a877-43e1-952d-90fdedfb6781';
  const input: CustomerInput = { name: 'Example', tradeName: null, customerType: 'Individual', taxId: '001.2', email: 'A@example.test' };
  const write = { ...input, id, version };
  const customer: Customer = { ...write, active: false };
  let api: CustomerService;
  let http: HttpTestingController;
  let auth: AuthState;
  let url: string;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
    });
    api = TestBed.inject(CustomerService);
    http = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthState);
    url = TestBed.inject(ApiConfig).customersUrl;
    auth.start('test-token', Date.now() + 60_000, () => {});
  });

  afterEach(() => { http.verify(); auth.clear(); });

  function expectCustomerRequest(method: string, path = url) {
    const request = http.expectOne({ method, url: path });
    expect(request.request.headers.get('Authorization')).toBe('Bearer test-token');
    return request;
  }

  it('returns all customers and reads an inactive customer by ID', () => {
    api.list().subscribe((items) => expect(items).toEqual([customer]));
    expectCustomerRequest('GET').flush([customer]);
    api.getById(id).subscribe((item) => expect(item).toEqual(customer));
    expectCustomerRequest('GET', url + '/' + id).flush(customer);
  });

  it('sends only editable fields on create and the expected version on update', () => {
    api.create(input).subscribe((response) => expect(response.body).toEqual(write));
    const post = expectCustomerRequest('POST');
    expect(post.request.body).toEqual(input);
    post.flush(write, { status: 201, statusText: 'Created' });
    api.update(id, { ...input, version }).subscribe((response) => expect(response.body).toEqual(write));
    const put = expectCustomerRequest('PUT', url + '/' + id);
    expect(put.request.body).toEqual({ ...input, version });
    put.flush(write);
  });

  it('sends the expected version in the DELETE query without a body', () => {
    api.deactivate(id, version).subscribe((response) => expect(response.status).toBe(204));
    const request = expectCustomerRequest('DELETE', url + '/' + id + '?version=' + version);
    expect(request.request.body).toBeNull();
    expect(request.request.params.get('version')).toBe(version);
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('propagates write errors without retrying', () => {
    const failure = jasmine.createSpy('failure');
    api.update(id, { ...input, version }).subscribe({ error: failure });
    expectCustomerRequest('PUT', url + '/' + id).flush({}, { status: 409, statusText: 'Conflict' });
    expect(failure).toHaveBeenCalledOnceWith(jasmine.objectContaining({ status: 409 }));
    http.expectNone({ method: 'PUT' });
  });
});
