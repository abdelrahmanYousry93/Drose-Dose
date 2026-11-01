import { Routes } from '@angular/router';
import { AdminLogin, AdminPayments, AdminSettings, AdminShell, adminGuard } from './admin';

export const adminRoutes: Routes = [
  { path: 'login', component: AdminLogin, title: 'دخول الإدارة — Dose' },
  {
    path: '',
    component: AdminShell,
    canActivate: [adminGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard', title: 'الرئيسية — Dose',
        loadComponent: () => import('./admin-dashboard').then((m) => m.AdminDashboard),
      },
      {
        path: 'users', title: 'المستخدمين — Dose',
        loadComponent: () => import('./admin-users').then((m) => m.AdminUsers),
      },
      {
        path: 'availability', title: 'مواعيد المدرسين — Dose',
        loadComponent: () => import('./admin-users').then((m) => m.AdminAvailability),
      },
      { path: 'payments', component: AdminPayments, title: 'مراجعة المدفوعات — Dose' },
      {
        path: 'sessions', title: 'الجلسات — Dose',
        loadComponent: () => import('./admin-sessions').then((m) => m.AdminSessions),
      },
      {
        path: 'pricing', title: 'الأسعار — Dose',
        loadComponent: () => import('./admin-pricing').then((m) => m.AdminPricing),
      },
      {
        path: 'teachers', title: 'المدرسين — Dose',
        loadComponent: () => import('./admin-config').then((m) => m.AdminTeachers),
      },
      {
        path: 'catalog', title: 'الكتالوج — Dose',
        loadComponent: () => import('./admin-config').then((m) => m.AdminCatalog),
      },
      {
        path: 'payment-methods', title: 'طرق الدفع — Dose',
        loadComponent: () => import('./admin-config').then((m) => m.AdminPaymentMethods),
      },
      { path: 'settings', component: AdminSettings, title: 'الإعدادات — Dose' },
      {
        path: 'follow-ups', title: 'المتابعات — Dose',
        loadComponent: () => import('./admin-followups').then((m) => m.AdminFollowUps),
      },
      {
        path: 'requests', title: 'طلبات الجلسات — Dose',
        loadComponent: () => import('./admin-followups').then((m) => m.AdminRequests),
      },
    ],
  },
];
