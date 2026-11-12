import { Component, DestroyRef, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { clubContent } from '../../content/club-content';

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="public-shell">
      <a class="skip-link" href="#main-content">Skip to content</a>
      <header class="public-header" (keydown.escape)="menuOpen.set(false)">
        <div class="nav-inner">
          <a routerLink="/" class="brand" aria-label="Es’hlahleni Social Club home">
            <img src="assets/branding/eshlahleni-favicon.png" alt="" width="48" height="48" />
            <span class="brand-name">Es’hlahleni<small>SOCIAL CLUB · EST. 2025</small></span>
          </a>
          <nav id="public-navigation" class="public-nav" [class.is-open]="menuOpen()" aria-label="Main navigation">
            <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{exact: true}" ariaCurrentWhenActive="page">Home</a>
            <a routerLink="/about" routerLinkActive="active" ariaCurrentWhenActive="page">The club</a>
            <a routerLink="/membership" routerLinkActive="active" ariaCurrentWhenActive="page">Membership</a>
            <a routerLink="/faq" routerLinkActive="active" ariaCurrentWhenActive="page">House rules & FAQ</a>
            <a routerLink="/contact" routerLinkActive="active" ariaCurrentWhenActive="page">Contact</a>
          </nav>
          <a class="button button--olive nav-join" routerLink="/join">Join the club <span aria-hidden="true">↗</span></a>
          <button class="menu-toggle" type="button" aria-controls="public-navigation" [attr.aria-expanded]="menuOpen()" (click)="menuOpen.set(!menuOpen())">{{ menuOpen() ? 'Close' : 'Menu' }}</button>
        </div>
      </header>
      <main id="main-content" tabindex="-1"><router-outlet /></main>
      <footer class="public-footer">
        <div class="footer-grid">
          <div class="footer-brand">
            <img src="assets/branding/eshlahleni-logo.jpeg" alt="Es’hlahleni Social Club emblem, established 2025" width="105" height="117" loading="lazy" />
            <div><strong>ES'HLAHLENI<br />SOCIAL CLUB</strong><p>{{ content.motto }}</p></div>
          </div>
          <div><span class="footer-label">THE CLUB</span><div class="footer-links"><a routerLink="/about">Our story</a><a routerLink="/membership">Membership</a><a routerLink="/faq">House rules & FAQ</a><a routerLink="/contact">Get in touch</a></div></div>
          <div><span class="footer-label">MEMBERSHIP STARTS HERE</span><p>@for (line of content.contact.address; track line) { {{ line }}<br /> }</p><a [href]="content.contact.whatsAppUrl">WhatsApp {{ content.contact.whatsAppNumber }}</a><p class="footer-description">Register online. Come through. <br />Complete the R100 step in person.</p><a class="text-link" routerLink="/join">Register for membership <span aria-hidden="true">↗</span></a><p class="footer-adult"><strong>18+ ONLY</strong> · 18+ registration.</p></div>
        </div>
        <div class="footer-bottom"><span>© {{ year }} Es'hlahleni Social Club.</span><div><a routerLink="/membership-terms">Membership terms</a><a routerLink="/privacy">Privacy notice</a><a routerLink="/admin/login">Admin sign in ↗</a></div><span class="provider">Website by iKhethelo Digital</span></div>
      </footer>
    </div>
  `,
})
export class PublicShell {
  readonly content = clubContent;
  readonly year = new Date().getFullYear();
  readonly menuOpen = signal(false);
  constructor() {
    inject(Router).events.pipe(filter(event => event instanceof NavigationEnd), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe(() => this.menuOpen.set(false));
  }
}
