import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { distinctUntilChanged, map } from 'rxjs';
import { CustomerDeactivate } from './customer-deactivate';
import { customerRead } from './customer-read';
import { CustomerService } from './customer.service';

@Component({
  imports: [RouterLink, MatButtonModule, CustomerDeactivate],
  templateUrl: './customer-details.html',
  styleUrl: './customers.css',
})
export class CustomerDetails {
  private readonly api = inject(CustomerService);
  private readonly id = inject(ActivatedRoute).paramMap.pipe(map((params) => params.get('id')!), distinctUntilChanged());
  readonly customer = customerRead(this.id, (id) => this.api.getById(id));
}
