import { Component, inject, signal } from "@angular/core";
import {
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
  Routes,
  CanDeactivateFn,
} from "@angular/router";
import { Auth, authGuard } from "./core/auth";
import { CONFIG } from "./core/config";

export interface DraftPage {
  hasUnsavedChanges(): boolean;
}
export const leaveDraft: CanDeactivateFn<DraftPage> = (page) =>
  !inject(Auth).signedIn() ||
  !page.hasUnsavedChanges() ||
  confirm("Leave without saving your changes?");
@Component({
  selector: "ntm-root",
  imports: [RouterOutlet],
  template: "<router-outlet />",
})
export class App {}
@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: ` <a class="skip-link" href="#main-content">Skip to main content</a>
    <div class="workspace">
      <aside class="sidebar" [class.open]="mobileOpen()">
        <a class="brand" routerLink="/dashboard" (click)="mobileOpen.set(false)"
          ><span class="brand-mark" aria-hidden="true">g<span>p</span></span
          ><span
            >NTM <strong>GreenPulse</strong
            ><small>NETWORK ADMINISTRATION</small></span
          ></a
        >
        <span class="nav-caption">WORKSPACE</span>
        <nav aria-label="Main navigation">
          @for (item of navigation; track item.path) {
            @if (item.path !== '/administrators' || auth.role() === 'TenantAdmin' || auth.role() === 'IKhetheloAdmin') {
            <a
              [routerLink]="item.path"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: item.path === '/dashboard' }"
              ariaCurrentWhenActive="page"
              (click)="mobileOpen.set(false)"
              ><span class="nav-symbol" aria-hidden="true">{{
                item.symbol
              }}</span
              >{{ item.label }}</a
            >
            }
          }
        </nav>
        <div class="sidebar-foot">
          <span class="network-dot"></span> InnoBiz DUT programme<small
            >Veteran entrepreneurship support</small
          >
        </div>
      </aside>
      @if (mobileOpen()) {
        <button
          class="sidebar-shade"
          aria-label="Close navigation"
          (click)="mobileOpen.set(false)"
        ></button>
      }
      <div class="workarea">
        <header class="topbar">
          <div class="d-flex align-items-center gap-3">
            <button
              class="btn btn-sm btn-outline-secondary mobile-menu"
              aria-label="Toggle navigation"
              [attr.aria-expanded]="mobileOpen()"
              (click)="mobileOpen.set(!mobileOpen())"
            >
              Menu</button
            ><span class="topbar-label">Veteran & business management</span>
          </div>
          <div class="d-flex align-items-center gap-3">
            <span class="tenant-label">{{ config.tenantCode }}</span
            ><button
              class="btn btn-sm btn-outline-secondary"
              (click)="signOut()"
            >
              Sign out
            </button>
          </div>
        </header>
        <main id="main-content" tabindex="-1"><router-outlet /></main>
        <footer>
          NTM GreenPulse Network
          <span>InnoBiz DUT entrepreneurship support programme</span>
        </footer>
      </div>
    </div>`,
})
export class Shell {
  readonly config = inject(CONFIG);
  readonly auth = inject(Auth);
  private readonly router = inject(Router);
  readonly mobileOpen = signal(false);
  readonly navigation = [
    { path: "/dashboard", label: "Overview", symbol: "◈" },
    { path: "/veterans", label: "Veterans", symbol: "○" },
    { path: "/businesses", label: "Businesses", symbol: "▤" },
    { path: "/administrators", label: "User Management", symbol: "⚙" },
    { path: "/account", label: "My Account", symbol: "○" },
  ];
  async signOut() {
    if (await this.router.navigateByUrl("/login")) this.auth.logout();
  }
}
export const routes: Routes = [
  {
    path: "login",
    title: "Sign in · NTM GreenPulse",
    loadComponent: () => import("./pages/login").then((m) => m.Login),
  },
  {
    path: "",
    component: Shell,
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: "", pathMatch: "full", redirectTo: "dashboard" },
      {
        path: "dashboard",
        title: "Overview · NTM GreenPulse",
        loadComponent: () =>
          import("./pages/dashboard").then((m) => m.DashboardPage),
      },
      {
        path: "veterans",
        title: "Veterans · NTM GreenPulse",
        loadComponent: () => import("./pages/veterans").then((m) => m.Veterans),
      },
      {
        path: "veterans/new",
        title: "New veteran · NTM GreenPulse",
        canDeactivate: [leaveDraft],
        loadComponent: () =>
          import("./pages/veteran-form").then((m) => m.VeteranForm),
      },
      {
        path: "veterans/:id",
        title: "Veteran details · NTM GreenPulse",
        canDeactivate: [leaveDraft],
        loadComponent: () =>
          import("./pages/veteran-form").then((m) => m.VeteranForm),
      },
      {
        path: "businesses",
        title: "Businesses · NTM GreenPulse",
        loadComponent: () =>
          import("./pages/businesses").then((m) => m.Businesses),
      },
      {
        path: "businesses/new",
        title: "New business · NTM GreenPulse",
        canDeactivate: [leaveDraft],
        loadComponent: () =>
          import("./pages/business-form").then((m) => m.BusinessForm),
      },
      {
        path: "businesses/:id",
        title: "Business assessment · NTM GreenPulse",
        canDeactivate: [leaveDraft],
        loadComponent: () =>
          import("./pages/business-form").then((m) => m.BusinessForm),
      },
      {
        path: "administrators",
        title: "User Management · NTM GreenPulse",
        canActivate: [() => ['TenantAdmin', 'IKhetheloAdmin'].includes(inject(Auth).role() ?? '') ? true : inject(Router).createUrlTree(['/dashboard'])],
        canDeactivate: [leaveDraft],
        loadComponent: () =>
          import("./pages/administrators").then((m) => m.Administrators),
      },
      { path: "account", title: "My Account · NTM GreenPulse", loadComponent: () => import("./pages/my-account").then(m => m.MyAccount) },
      { path: "**", redirectTo: "dashboard" },
    ],
  },
];
