import { Component, DestroyRef, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { AbstractControl, FormControl, FormGroup, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { distinctUntilChanged, finalize, map, of } from 'rxjs';
import { Customer, CustomerInput, CustomerType } from './customer.models';
import { CustomerProblem, customerProblem } from './customer-problem';
import { customerRead } from './customer-read';
import { CustomerService } from './customer.service';

function trimmed(validator: ValidatorFn): ValidatorFn {
  return (control) => validator(new FormControl(String(control.value ?? '').trim()));
}

@Component({
  imports: [ReactiveFormsModule, RouterLink, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  templateUrl: './customer-editor.html',
  styleUrl: './customers.css',
})
export class CustomerEditor {
  private readonly api = inject(CustomerService);
  private readonly router = inject(Router);
  private readonly snack = inject(MatSnackBar);
  private readonly destroy = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  private readonly routeId = this.route.paramMap.pipe(map((params) => params.get('id')), distinctUntilChanged());
  readonly id = toSignal(this.routeId, { requireSync: true });
  readonly read = customerRead(this.routeId, (id) => id === null ? of(null) : this.api.getById(id));
  readonly original = signal<Customer | null>(null);
  readonly editingLoadedCustomer = computed(() => {
    const id = this.id();
    return id !== null && this.original()?.id.toLowerCase() === id.toLowerCase();
  });
  readonly pending = signal(false);
  readonly problem = signal<CustomerProblem | null>(null);
  readonly confirmReload = signal(false);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [trimmed(Validators.required), trimmed(Validators.maxLength(200))] }),
    tradeName: new FormControl({ value: '', disabled: true }, { nonNullable: true, validators: trimmed(Validators.maxLength(200)) }),
    customerType: new FormControl<CustomerType | null>(null, Validators.required),
    taxId: new FormControl('', { nonNullable: true, validators: [trimmed(Validators.required), trimmed(Validators.maxLength(50))] }),
    email: new FormControl('', { nonNullable: true, validators: [trimmed(Validators.required), trimmed(Validators.email), trimmed(Validators.maxLength(254))] }),
  });

  constructor() {
    this.form.controls.customerType.valueChanges.pipe(takeUntilDestroyed()).subscribe((type) => this.setTradeNameAvailability(type));
    effect(() => {
      const state = this.read.state();
      if (state.kind !== 'ready') return;
      const record = state.value;
      this.original.set(record);
      this.problem.set(null);
      this.confirmReload.set(false);
      this.form.reset({
        name: record?.name ?? '',
        tradeName: record?.tradeName ?? '',
        customerType: record?.customerType ?? null,
        taxId: record?.taxId ?? '',
        email: record?.email ?? '',
      }, { emitEvent: false });
      this.setTradeNameAvailability(record?.customerType ?? null);
    });
  }

  error(control: AbstractControl, label: string, limit?: number): string {
    if (control.hasError('server')) return control.getError('server');
    if (control.hasError('required')) return label + ' is required.';
    if (control.hasError('maxlength')) return label + ' must be at most ' + limit + ' characters.';
    return 'Enter a valid email address.';
  }

  reload(): void {
    this.confirmReload.set(false);
    this.read.reload();
  }

  save(): void {
    const id = this.id();
    const original = this.original();
    if (this.pending() || this.read.state().kind !== 'ready' ||
        (id !== null && !this.editingLoadedCustomer()) || this.problem()?.kind) return;
    this.form.markAllAsTouched();
    const values = this.form.getRawValue();
    if (this.form.invalid || !values.customerType) return;

    const input: CustomerInput = {
      name: values.name.trim(),
      tradeName: values.customerType === 'Company' ? values.tradeName.trim() || null : null,
      customerType: values.customerType,
      taxId: values.taxId.trim(),
      email: values.email.trim(),
    };
    const request = original ? this.api.update(original.id, { ...input, version: original.version }) : this.api.create(input);
    this.problem.set(null);
    this.pending.set(true);
    this.form.disable({ emitEvent: false });
    request.pipe(
      takeUntilDestroyed(this.destroy),
      finalize(() => {
        this.form.enable({ emitEvent: false });
        this.setTradeNameAvailability(this.form.controls.customerType.value);
        // Enabling controls revalidates them, so attach server errors afterwards.
        const fields = this.problem()?.fields;
        if (fields) {
          for (const field of Object.keys(fields) as (keyof CustomerInput)[]) {
            const control = this.form.controls[field];
            if (control.enabled) control.setErrors({ ...control.errors, server: fields[field] });
          }
        }
        this.pending.set(false);
      }),
    ).subscribe({
      next: (response) => {
        if (this.id() !== id) return;
        if (response.status !== (original ? 200 : 201) || !response.body?.id) {
          this.problem.set({ message: 'The result could not be confirmed. Check the current server state.' });
          return;
        }
        this.snack.open(original ? 'Customer updated.' : 'Customer created.', 'Dismiss', { duration: 5000 });
        void this.router.navigate(['/customers', response.body.id]);
      },
      error: (error: unknown) => {
        if (this.id() === id) this.problem.set(customerProblem(error, true));
      },
    });
  }

  private setTradeNameAvailability(type: CustomerType | null): void {
    const control = this.form.controls.tradeName;
    if (type === 'Company') {
      control.enable({ emitEvent: false });
    } else {
      control.reset('', { emitEvent: false });
      control.disable({ emitEvent: false });
    }
  }
}
