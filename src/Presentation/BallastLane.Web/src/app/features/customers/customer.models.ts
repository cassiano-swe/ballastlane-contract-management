export type CustomerType = 'Individual' | 'Company';

export interface CustomerInput {
  name: string;
  tradeName: string | null;
  customerType: CustomerType;
  taxId: string;
  email: string;
}

export interface CustomerWriteResult extends CustomerInput {
  id: string;
  version: string;
}

export interface Customer extends CustomerWriteResult {
  active: boolean;
}

export interface CustomerUpdate extends CustomerInput {
  version: string;
}
