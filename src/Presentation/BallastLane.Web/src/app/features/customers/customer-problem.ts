import { HttpErrorResponse } from '@angular/common/http';
import { CustomerInput } from './customer.models';

export interface CustomerProblem {
  message: string;
  traceId?: string;
  kind?: 'stale' | 'missing';
  fields?: Partial<Record<keyof CustomerInput, string>>;
}

const fieldNames = ['name', 'tradeName', 'customerType', 'taxId', 'email'] as const;

export function customerProblem(error: unknown, mutation = false): CustomerProblem {
  const fallback = 'An unexpected error occurred.';
  if (!(error instanceof HttpErrorResponse)) return { message: fallback };
  const body: unknown = error.error;
  const problem = body && typeof body === 'object' ? body as Record<string, unknown> : {};
  const trace = problem['traceId'];
  const traceId = typeof trace === 'string' && /^[a-z\d:._-]{1,128}$/i.test(trace) ? trace : undefined;
  switch (error.status) {
    case 400: {
      const fields: CustomerProblem['fields'] = {};
      const errors = problem['errors'];
      if (errors && typeof errors === 'object') {
        for (const [key, messages] of Object.entries(errors)) {
          const field = fieldNames.find((name) => name.toLowerCase() === key.replace(/^\$\./, '').toLowerCase());
          // JSON binding messages can include parser internals. Use fixed copy for those keys.
          if (field && Array.isArray(messages) && messages.length > 0) {
            const message = messages[0];
            fields[field] = !key.startsWith('$.') && typeof message === 'string' &&
              isSafeValidationMessage(message)
              ? message : 'Check this value.';
          }
        }
      }
      return { message: mutation ? 'Check the submitted information and try again.' : 'The customer identifier is invalid.', fields, traceId };
    }
    case 401:
      return { message: 'Sign in to continue.', traceId };
    case 403:
      return { message: 'Access denied.', traceId };
    case 404:
      return { message: 'This customer no longer exists.', kind: 'missing', traceId };
    case 409:
      switch (problem['type']) {
        case 'urn:ballastlane:customer-duplicate-tax-id':
          return { message: 'This tax ID is already in use.', fields: { taxId: 'This tax ID is already in use.' }, traceId };
        case 'urn:ballastlane:customer-concurrency-conflict':
          return { message: 'Another operation changed this customer. Reload the latest data before trying again.', kind: 'stale', traceId };
        default:
          return { message: 'The request conflicts with the current server state.', traceId };
      }
    case 429:
      return { message: 'Too many requests. Wait a moment before trying again.', traceId };
    default:
      return {
        message: mutation && (error.status === 0 || error.status >= 500)
          ? 'The result could not be confirmed. Check the current server state before trying again.'
          : 'The request failed. Try again later.',
        traceId,
      };
  }
}

function isSafeValidationMessage(message: string): boolean {
  return message === 'Trade name is only allowed for companies.' || message === 'A valid email is required.' ||
    /^'(Name|Trade Name|Customer Type|Tax Id|Email)' must not be empty\.$/i.test(message) ||
    /^The length of '(Name|Trade Name|Tax Id|Email)' must be \d+ characters or fewer\. You entered \d+ characters\.$/i.test(message);
}
