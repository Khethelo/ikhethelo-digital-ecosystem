import { Routes } from '@angular/router';
import { adminChildGuard, authGuard } from './core/auth.guard';

export const routes: Routes = [
  { path: 'admin/login', title: 'Administrator sign in', data: { noIndex: true }, loadComponent: () => import('./features/admin/login').then(module => module.LoginPage) },
  { path: 'admin', canActivate: [authGuard], canActivateChild: [adminChildGuard], loadComponent: () => import('./features/admin/admin-shell').then(module => module.AdminShell), children: [
    { path: '', pathMatch: 'full', title: 'Club overview', loadComponent: () => import('./features/admin/dashboard').then(module => module.DashboardPage) },
    { path: 'applications', title: 'Membership applications', loadComponent: () => import('./features/admin/applications').then(module => module.ApplicationsPage) },
    { path: 'applications/:applicationId', title: 'Review application', loadComponent: () => import('./features/admin/application-detail').then(module => module.ApplicationDetailPage) },
    { path: 'members', title: 'Club members', loadComponent: () => import('./features/admin/members').then(module => module.MembersPage) },
    { path: 'members/:memberId', title: 'Member details', loadComponent: () => import('./features/admin/member-detail').then(module => module.MemberDetailPage) },
    { path: '**', redirectTo: '/admin' },
  ] },
  { path: '', loadComponent: () => import('./features/public/public-shell').then(module => module.PublicShell), children: [
    { path: '', pathMatch: 'full', title: 'Come through. Kick back.', data: { description: 'Cannabis, PS5 and good company in Durban Central. Get to know Es’hlahleni Social Club and start your membership registration. 18+.' }, loadComponent: () => import('./features/public/home').then(module => module.HomePage) },
    { path: 'about', title: 'About the club', data: { description: 'Meet Es’hlahleni Social Club: a cannabis-centred social club with PS5, good company and good vibes.' }, loadComponent: () => import('./features/public/info-pages').then(module => module.AboutPage) },
    { path: 'membership', title: 'How membership works', data: { description: 'Understand online registration and in-person completion for Es’hlahleni Social Club membership.' }, loadComponent: () => import('./features/public/info-pages').then(module => module.MembershipPage) },
    { path: 'join', title: 'Register for membership', data: { description: 'Apply for Es’hlahleni Social Club membership online. No applicant account required. Applicants must be 18 or older.' }, loadComponent: () => import('./features/public/join').then(module => module.JoinPage) },
    { path: 'faq', title: 'House rules and FAQ', data: { description: 'Answers to membership questions and an introduction to the Es’hlahleni club experience.' }, loadComponent: () => import('./features/public/info-pages').then(module => module.FaqPage) },
    { path: 'contact', title: 'Contact the club', data: { description: 'Contact and location information for Es’hlahleni Social Club.' }, loadComponent: () => import('./features/public/info-pages').then(module => module.ContactPage) },
    { path: 'membership-terms', title: 'Membership terms — preview', data: { noIndex: true }, loadComponent: () => import('./features/public/info-pages').then(module => module.TermsPage) },
    { path: 'privacy', title: 'Privacy notice — preview', data: { noIndex: true }, loadComponent: () => import('./features/public/info-pages').then(module => module.PrivacyPage) },
    { path: '**', title: 'Page not found', data: { noIndex: true }, loadComponent: () => import('./features/public/info-pages').then(module => module.NotFoundPage) },
  ] },
];
