import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatSidenavModule } from '@angular/material/sidenav';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../core/authentication/auth.service';

@Component({
  imports: [MatButtonModule, MatSidenavModule, RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './main-layout.html',
  styleUrl: './main-layout.css',
})
export class MainLayout {
  readonly auth = inject(AuthService);
  readonly narrow = toSignal(
    inject(BreakpointObserver).observe('(max-width: 767px)').pipe(map((state) => state.matches)),
    { initialValue: false },
  );
}
