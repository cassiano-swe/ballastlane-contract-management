import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { firstValueFrom } from 'rxjs';
import { ApiConfig } from '../../core/config/api-config';
import { Customer } from './customer.models';
import { CustomerEditor } from './customer-editor';
import { customerRoutes } from './customers.routes';

describe('Customer screens', () => {
  const record: Customer = {
    id: '6d8df8a5-8ef2-4c6e-90f5-1dc89e923c17', name: 'Example customer', tradeName: 'Example shop',
    customerType: 'Company', taxId: '001.2', email: 'A@example.test', active: true,
    version: 'c53db246-a877-43e1-952d-90fdedfb6781',
  };
  const latest = { ...record, name: 'New server value', version: 'a7280e43-7c4d-4d18-a09e-8c3a214e6732' };
  let http: HttpTestingController;
  let url: string;
  let item: string;
  let harness: RouterTestingHarness;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'customers', children: customerRoutes }]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    url = TestBed.inject(ApiConfig).customersUrl;
    item = url + '/' + record.id;
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => { TestBed.inject(MatDialog).closeAll(); http.verify(); });

  function click(label: string, scope: ParentNode = harness.routeNativeElement!): void {
    const button = Array.from(scope.querySelectorAll<HTMLButtonElement>('button')).find((el) => el.textContent?.trim() === label)!;
    expect(button).toBeDefined();
    button.click();
    harness.detectChanges();
  }

  it('distinguishes loading, failed and empty lists and preserves inactive rows', async () => {
    await harness.navigateByUrl('/customers');
    const page = harness.routeNativeElement!;
    expect(page.textContent).toContain('Loading customers');
    http.expectOne(url).flush(null, { status: 500, statusText: 'Error' });
    harness.detectChanges();
    expect(page.textContent).not.toContain('No customers yet');
    click('Retry');
    http.expectOne(url).flush([]);
    harness.detectChanges();
    expect(page.textContent).toContain('No customers yet');
    await harness.navigateByUrl('/customers/' + record.id);
    http.expectOne(item).flush({ ...record, active: false });
    harness.detectChanges();
    expect(harness.routeNativeElement?.textContent).toContain('Inactive');
    expect(harness.routeNativeElement?.querySelector<HTMLButtonElement>('app-customer-deactivate button')?.disabled).toBeTrue();
    await harness.navigateByUrl('/customers');
    http.expectOne(url).flush([record, { ...record, id: latest.version, active: false, name: 'Second' }]);
    harness.detectChanges();
    const rows = harness.routeNativeElement!.querySelectorAll('tbody tr');
    expect(rows[0].textContent).toContain(record.name);
    expect(rows[1].textContent).toContain('Inactive');
    expect(rows[1].querySelector<HTMLButtonElement>('button')?.disabled).toBeTrue();
    expect(rows[1].querySelector('a[aria-label="Edit Second"]')).not.toBeNull();
  });

  it('shows a missing customer with a route back to the list', async () => {
    await harness.navigateByUrl('/customers/' + record.id);
    http.expectOne(item).flush({}, { status: 404, statusText: 'Not Found' });
    harness.detectChanges();
    expect(harness.routeNativeElement?.textContent).toContain('This customer no longer exists');
    expect(harness.routeNativeElement?.querySelector('a')?.getAttribute('href')).toBe('/customers');
  });

  it('shows an invalid item URL without making an unauthenticated request', async () => {
    await harness.navigateByUrl('/customers/invalid-id');
    http.expectNone(url + '/invalid-id');
    expect(harness.routeNativeElement?.textContent).toContain('The customer identifier is invalid.');
  });

  it('maps server validation keys and keeps the draft after a rejected create', async () => {
    const editor = await harness.navigateByUrl('/customers/new', CustomerEditor);
    editor.form.patchValue({ name: 'Draft', customerType: 'Individual', taxId: '003', email: 'test@example.test' });
    editor.save();
    http.expectOne({ method: 'POST', url }).flush({
      errors: { Email: ['A valid email is required.'], '$.taxId': ['Unsafe parser detail.'] },
      traceId: 'test-trace:1',
    }, { status: 400, statusText: 'Bad Request' });
    harness.detectChanges();
    expect(editor.form.controls.email.getError('server')).toBe('A valid email is required.');
    expect(editor.form.controls.taxId.getError('server')).toBe('Check this value.');
    expect(editor.form.controls.name.value).toBe('Draft');
    expect(harness.routeNativeElement?.textContent).toContain('test-trace:1');
    expect(harness.routeNativeElement?.textContent).not.toContain('Unsafe parser detail.');
  });

  it('validates and normalizes creation, submits once and reads persisted details', async () => {
    const editor = await harness.navigateByUrl('/customers/new', CustomerEditor);
    editor.save();
    http.expectNone(url);
    editor.form.setValue({ name: ' '.repeat(3), tradeName: '', customerType: 'Company', taxId: '001', email: 'invalid' });
    expect(editor.form.controls.name.hasError('required')).toBeTrue();
    expect(editor.form.controls.email.hasError('email')).toBeTrue();
    editor.form.controls.name.setValue('x'.repeat(201));
    expect(editor.form.controls.name.hasError('maxlength')).toBeTrue();
    editor.form.controls.tradeName.setValue('Old trade name');
    editor.form.controls.customerType.setValue('Individual');
    expect(editor.form.controls.tradeName.disabled).toBeTrue();
    expect(editor.form.controls.tradeName.value).toBe('');
    editor.form.patchValue({ name: ' Example ', taxId: ' 001.2 ', email: ' A@example.test ' });
    editor.save();
    editor.save();
    const request = http.expectOne({ method: 'POST', url });
    expect(request.request.body).toEqual({ name: 'Example', tradeName: null, customerType: 'Individual', taxId: '001.2', email: 'A@example.test' });
    request.flush({ id: record.id }, { status: 201, statusText: 'Created' });
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/customers/' + record.id);
    http.expectOne(item).flush(record);
  });

  it('preserves a draft across errors and requires explicit reload and a new submit after stale edits', async () => {
    const editor = await harness.navigateByUrl('/customers/' + record.id + '/edit', CustomerEditor);
    http.expectOne(item).flush({ ...record, active: false });
    harness.detectChanges();
    expect(editor.form.controls.tradeName.value).toBe(record.tradeName!);
    editor.form.controls.name.setValue('Draft');
    editor.save();
    http.expectOne({ method: 'PUT', url: item }).flush({
      type: 'urn:ballastlane:customer-duplicate-tax-id',
    }, { status: 409, statusText: 'Conflict' });
    expect(editor.form.controls.taxId.getError('server')).toBe('This tax ID is already in use.');
    expect(editor.form.controls.name.value).toBe('Draft');
    editor.form.controls.taxId.setValue('009');
    editor.save();
    const stale = http.expectOne({ method: 'PUT', url: item });
    expect(stale.request.body.version).toBe(record.version);
    stale.flush({ type: 'urn:ballastlane:customer-concurrency-conflict' }, { status: 409, statusText: 'Conflict' });
    harness.detectChanges();
    expect(editor.form.controls.name.value).toBe('Draft');
    editor.save();
    click('Reload latest customer');
    http.expectNone(item);
    click('Discard changes and reload');
    http.expectOne({ method: 'GET', url: item }).flush(latest);
    harness.detectChanges();
    http.expectNone(item);
    expect(editor.form.controls.name.value).toBe(latest.name);
    editor.save();
    const retry = http.expectOne({ method: 'PUT', url: item });
    expect(retry.request.body.version).toBe(latest.version);
    retry.flush(latest);
    await harness.fixture.whenStable();
    expect(TestBed.inject(Router).url).toBe('/customers/' + record.id);
    http.expectOne({ method: 'GET', url: item }).flush(latest);
  });

  it('cancels deactivation, handles stale versions and refreshes on 204', async () => {
    const dialogs = TestBed.inject(MatDialog);
    await harness.navigateByUrl('/customers/' + record.id);
    http.expectOne(item).flush(record);
    harness.detectChanges();
    click('Deactivate');
    let closed = firstValueFrom(dialogs.afterAllClosed);
    click('Cancel', document);
    await closed;
    harness.detectChanges();
    http.expectNone({ method: 'DELETE' });
    click('Deactivate');
    click('Deactivate', document.querySelector('mat-dialog-container')!);
    http.expectOne({ method: 'DELETE', url: item + '?version=' + record.version }).flush({
      type: 'urn:ballastlane:customer-concurrency-conflict',
    }, { status: 409, statusText: 'Conflict' });
    await harness.fixture.whenStable();
    http.expectNone(item);
    http.expectNone({ method: 'DELETE' });
    closed = firstValueFrom(dialogs.afterAllClosed);
    click('Reload latest data', document);
    await closed;
    http.expectOne({ method: 'GET', url: item }).flush(latest);
    harness.detectChanges();
    click('Deactivate');
    click('Deactivate', document.querySelector('mat-dialog-container')!);
    closed = firstValueFrom(dialogs.afterAllClosed);
    http.expectOne({ method: 'DELETE', url: item + '?version=' + latest.version }).flush(null, { status: 204, statusText: 'No Content' });
    await closed;
    http.expectOne({ method: 'GET', url: item }).flush({ ...latest, active: false });
    harness.detectChanges();
    expect(harness.routeNativeElement?.textContent).toContain('Inactive');
  });
});
