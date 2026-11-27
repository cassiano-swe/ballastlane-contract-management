import { toSignal } from '@angular/core/rxjs-interop';
import { Observable, Subject, catchError, map, of, startWith, switchMap } from 'rxjs';
import { CustomerProblem, customerProblem } from './customer-problem';

type ReadState<T> = { kind: 'loading' } | { kind: 'ready'; value: T } | { kind: 'error'; problem: CustomerProblem };
const loading = { kind: 'loading' } as const;

export function customerRead<K, T>(key: Observable<K>, load: (key: K) => Observable<T>) {
  const reloads = new Subject<void>();
  const state = toSignal(key.pipe(switchMap((current) => reloads.pipe(
    startWith(undefined),
    switchMap(() => load(current).pipe(
      map((value): ReadState<T> => ({ kind: 'ready', value })),
      catchError((error: unknown) => of({ kind: 'error', problem: customerProblem(error) } as const)),
      startWith(loading),
    )),
  ))), { initialValue: loading });
  return { state, reload: () => reloads.next() };
}
