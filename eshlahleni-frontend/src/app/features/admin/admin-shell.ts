import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';

@Component({
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `<a class="skip-link" href="#main-content">Skip to administration</a><div class="admin-shell">
    <aside class="admin-sidebar"><a class="brand" routerLink="/admin" aria-label="Es’hlahleni administration overview"><img src="assets/branding/eshlahleni-logo.jpeg" alt="" width="50" height="56" /><span class="brand-name">ES'HLAHLENI<small>CLUB ADMINISTRATION</small></span></a>
      <span class="sidebar-label">MEMBERSHIP WORKSPACE</span><nav class="admin-nav" aria-label="Administration navigation"><a routerLink="/admin" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" ariaCurrentWhenActive="page"><span aria-hidden="true">▦</span> Overview</a><a routerLink="/admin/applications" routerLinkActive="active" ariaCurrentWhenActive="page"><span aria-hidden="true">▤</span> Membership Applications</a><a routerLink="/admin/applications" [queryParams]="{status:'Pending'}">Pending</a><a routerLink="/admin/applications" [queryParams]="{status:'Approved'}">Approved</a><a routerLink="/admin/applications" [queryParams]="{status:'Rejected'}">Declined</a><a routerLink="/admin/members" routerLinkActive="active" ariaCurrentWhenActive="page"><span aria-hidden="true">◎</span> Members</a>@if (auth.role() === 'TenantAdmin') { <a routerLink="/admin/users" routerLinkActive="active">User Management</a> }<a routerLink="/admin/account" routerLinkActive="active">My Account</a></nav>
      <div class="sidebar-bottom"><span class="workspace-badge"><span aria-hidden="true"></span> ES'HLAHLENI · ESHL</span><a routerLink="/">Visit public website ↗</a><button type="button" (click)="auth.logout()">Sign out <span aria-hidden="true">↗</span></button></div>
    </aside>
    <div class="admin-workspace"><header class="admin-topbar"><span>ES'HLAHLENI <span class="topbar-slash">/</span> <strong>MEMBERSHIP ADMINISTRATION</strong></span><button class="text-button mobile-sign-out" type="button" (click)="auth.logout()">Sign out</button><span class="admin-session-label"><span aria-hidden="true">●</span> Club administrator</span></header><main id="main-content" class="admin-main" tabindex="-1">@if (auth.accessDeniedMessage()) { <div class="alert alert--error" role="alert">{{ auth.accessDeniedMessage() }} <button class="text-button" type="button" (click)="auth.accessDeniedMessage.set('')">Dismiss</button></div> }<router-outlet /></main><footer class="admin-footer"><span>Es'hlahleni Social Club · Administration</span><span>All displayed times are South Africa time (UTC+2).</span></footer></div>
  </div>`,
})
export class AdminShell { readonly auth = inject(AuthService); }
