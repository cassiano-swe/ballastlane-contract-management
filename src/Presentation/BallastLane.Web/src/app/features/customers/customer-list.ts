import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { CustomerDeactivate } from './customer-deactivate';
import { customerRead } from './customer-read';
import { CustomerService } from './customer.service';

@Component({
  imports: [MatButtonModule, MatTableModule, RouterLink, CustomerDeactivate],
  templateUrl: './customer-list.html',
  styleUrl: './customers.css',
})
export class CustomerList {
  private readonly api = inject(CustomerService);
  readonly customers = customerRead(of(undefined), () => this.api.list());
  readonly columns = ['name', 'tradeName', 'customerType', 'taxId', 'email', 'status', 'actions'];
}
