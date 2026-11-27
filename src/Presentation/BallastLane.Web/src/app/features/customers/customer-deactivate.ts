import { Component, DestroyRef, inject, output, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { finalize } from 'rxjs';
import { Customer } from './customer.models';
import { CustomerProblem, customerProblem } from './customer-problem';
import { CustomerService } from './customer.service';

@Component({
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>Deactivate customer?</h2>
    <mat-dialog-content>
      <p><strong>{{ customer.name }}</strong> will be marked inactive. The record will be retained.</p>
      @if (pending()) { <p role="status">Deactivating…</p> }
      @if (problem(); as failure) {
        <p role="alert">{{ failure.message }}
          @if (failure.traceId) { <small>Support reference: {{ failure.traceId }}</small> }
        </p>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="undefined" [disabled]="pending()">Cancel</button>
      @if (problem()?.kind === 'stale') {
        <button mat-flat-button [mat-dialog-close]="'reload'">Reload latest data</button>
      } @else {
        <button mat-flat-button (click)="confirm()" [disabled]="pending() || problem()?.kind === 'missing'">Deactivate</button>
      }
    </mat-dialog-actions>
  `,
})
export class CustomerDeactivationDialog {
  private readonly api = inject(CustomerService);
  private readonly dialog = inject<MatDialogRef<CustomerDeactivationDialog, 'done' | 'reload'>>(MatDialogRef);
  private readonly destroy = inject(DestroyRef);
  readonly customer = inject<Customer>(MAT_DIALOG_DATA);
  readonly pending = signal(false);
  readonly problem = signal<CustomerProblem | null>(null);

  confirm(): void {
    const kind = this.problem()?.kind;
    if (this.pending() || kind === 'stale' || kind === 'missing') return;
    this.problem.set(null);
    this.pending.set(true);
    this.dialog.disableClose = true;
    this.api.deactivate(this.customer.id, this.customer.version).pipe(
      takeUntilDestroyed(this.destroy),
      finalize(() => {
        this.pending.set(false);
        this.dialog.disableClose = false;
      }),
    ).subscribe({
      next: (response) => {
        if (response.status === 204) this.dialog.close('done');
        else this.problem.set({ message: 'The result could not be confirmed. Check the current server state.' });
      },
      error: (error: unknown) => this.problem.set(customerProblem(error, true)),
    });
  }
}

@Component({
  selector: 'app-customer-deactivate',
  imports: [MatButtonModule],
  template: `<button mat-stroked-button type="button" [disabled]="!customer().active"
    [attr.aria-label]="'Deactivate ' + customer().name" (click)="showDialog()">Deactivate</button>`,
})
export class CustomerDeactivate {
  private readonly dialog = inject(MatDialog);
  private readonly snack = inject(MatSnackBar);
  private readonly destroy = inject(DestroyRef);
  readonly customer = input.required<Customer>();
  readonly refresh = output<void>();
  private readonly open = signal(false);

  showDialog(): void {
    if (!this.customer().active || this.open()) return;
    this.open.set(true);
    const dialog = this.dialog.open<CustomerDeactivationDialog, Customer, 'done' | 'reload'>(
      CustomerDeactivationDialog, { data: this.customer(), autoFocus: 'first-tabbable' },
    );
    const unregister = this.destroy.onDestroy(() => dialog.close());
    dialog.afterClosed().pipe(takeUntilDestroyed(this.destroy)).subscribe((result) => {
      unregister();
      this.open.set(false);
      if (result === 'done') this.snack.open('Customer deactivated.', 'Dismiss', { duration: 5000 });
      if (result) this.refresh.emit();
    });
  }
}
