import { Routes } from '@angular/router';
import { AdminLogin, AdminPayments, AdminSettings, AdminShell, adminGuard } from './admin';

export const adminRoutes: Routes = [
  { path: 'login', component: AdminLogin, title: 'titles.adminLogin' },
  {
    path: '',
    component: AdminShell,
    canActivate: [adminGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard', title: 'titles.adminDashboard',
        loadComponent: () => import('./admin-dashboard').then((m) => m.AdminDashboard),
      },
      {
        path: 'users', title: 'titles.adminUsers',
        loadComponent: () => import('./admin-users').then((m) => m.AdminUsers),
      },
      {
        path: 'availability', title: 'titles.adminAvailability',
        loadComponent: () => import('./admin-users').then((m) => m.AdminAvailability),
      },
      { path: 'payments', component: AdminPayments, title: 'titles.adminPayments' },
      {
        path: 'sessions', title: 'titles.adminSessions',
        loadComponent: () => import('./admin-sessions').then((m) => m.AdminSessions),
      },
      {
        path: 'pricing', title: 'titles.adminPricing',
        loadComponent: () => import('./admin-pricing').then((m) => m.AdminPricing),
      },
      {
        path: 'teachers', title: 'titles.adminTeachers',
        loadComponent: () => import('./admin-config').then((m) => m.AdminTeachers),
      },
      {
        path: 'catalog', title: 'titles.adminCatalog',
        loadComponent: () => import('./admin-config').then((m) => m.AdminCatalog),
      },
      {
        path: 'payment-methods', title: 'titles.adminPaymentMethods',
        loadComponent: () => import('./admin-config').then((m) => m.AdminPaymentMethods),
      },
      { path: 'settings', component: AdminSettings, title: 'titles.adminSettings' },
      {
        path: 'follow-ups', title: 'titles.adminFollowUps',
        loadComponent: () => import('./admin-followups').then((m) => m.AdminFollowUps),
      },
      {
        path: 'messages', title: 'titles.adminMessages',
        loadComponent: () => import('./admin-messages').then((m) => m.AdminMessages),
      },
      {
        path: 'promos', title: 'titles.adminPromos',
        loadComponent: () => import('./admin-promos').then((m) => m.AdminPromos),
      },
      {
        path: 'requests', title: 'titles.adminRequests',
        loadComponent: () => import('./admin-followups').then((m) => m.AdminRequests),
      },
    ],
  },
];
