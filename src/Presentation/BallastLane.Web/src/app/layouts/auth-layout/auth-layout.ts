import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  imports: [RouterOutlet],
  template: `
    <main>
      <section aria-labelledby="login-title">
        <header><span>BallastLane</span><p>Contract management workspace</p></header>
        <router-outlet />
      </section>
    </main>
  `,
  styles: `
    main { min-height: 100dvh; display: grid; place-items: center; padding: 1rem; }
    section { width: min(100%, 28rem); background: var(--surface); border: 1px solid var(--border);
      border-radius: 1rem; overflow: hidden; box-shadow: 0 16px 40px #10213b14; }
    header { padding: 1.4rem clamp(1rem, 5vw, 2rem); background: var(--navy); color: white; }
    header span { font-size: 1.25rem; font-weight: 750; }
    header p { color: #c8d9ed; font-size: .875rem; }
  `,
})
export class AuthLayout {}
