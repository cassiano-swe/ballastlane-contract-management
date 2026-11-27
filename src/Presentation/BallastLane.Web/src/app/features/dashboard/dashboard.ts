import { Component } from '@angular/core';

@Component({
  template: `
    <h1>Dashboard</h1>
    <p class="intro">Welcome to the BallastLane contract management workspace.</p>
    <section aria-labelledby="module-status">
      <h2 id="module-status">Module status</h2>
      <dl>
        <div><dt>Customers</dt><dd>Customer screens are upcoming.</dd></div>
        <div><dt>Contracts</dt><dd>Contract management is planned.</dd></div>
      </dl>
    </section>
  `,
  styles: `
    :host { display: block; max-width: 54rem; }
    h1 { font-size: clamp(1.8rem, 4vw, 2.5rem); letter-spacing: -.035em; }
    .intro { margin-block: .4rem 1.6rem; color: var(--muted); }
    section { background: var(--surface); border: 1px solid var(--border); border-radius: .75rem;
      padding: clamp(1rem, 3vw, 1.5rem); }
    h2 { font-size: 1.2rem; }
    dl { margin-block: .75rem 0; }
    dl div { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 13rem), 1fr));
      gap: .25rem 1rem; border-top: 1px solid var(--border); padding-block: .75rem; }
    dt { font-weight: 650; }
    dd { margin: 0; color: var(--muted); }
  `,
})
export class Dashboard {}
