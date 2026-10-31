import { Routes } from '@angular/router';
import { AdminLogin, AdminPayments, AdminSettings, AdminShell, adminGuard } from './admin';

export const adminRoutes: Routes = [
  { path: 'login', component: AdminLogin, title: 'دخول الإدارة — Dose' },
  {
    path: '',
    component: AdminShell,
    canActivate: [adminGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'payments' },
      { path: 'payments', component: AdminPayments, title: 'مراجعة المدفوعات — Dose' },
      { path: 'settings', component: AdminSettings, title: 'الإعدادات — Dose' },
    ],
  },
];
