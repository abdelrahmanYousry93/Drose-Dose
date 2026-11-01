import { Routes } from '@angular/router';
import { Home } from './features/home/home';

export const routes: Routes = [
  { path: '', component: Home, title: 'Dose دروس — جرعتك المركزة من الفهم' },
  {
    path: 'sessions',
    loadComponent: () => import('./features/sessions/sessions-list').then((m) => m.SessionsList),
    title: 'الجلسات المتاحة — Dose',
  },
  {
    path: 'sessions/:id',
    loadComponent: () => import('./features/sessions/session-details').then((m) => m.SessionDetailsPage),
    title: 'تفاصيل الجلسة — Dose',
  },
  {
    path: 'open-groups',
    loadComponent: () => import('./features/sessions/sessions-list').then((m) => m.SessionsList),
    data: { fixedMode: 'Group' },
    title: 'جلسات جماعية مفتوحة — Dose',
  },
  {
    // Secret booking link: /b/<token>. The token is the only key to the booking.
    path: 'b/:token',
    loadComponent: () => import('./features/booking/booking-page').then((m) => m.BookingPage),
    title: 'حجزي — Dose',
  },
  {
    path: 'request-session',
    loadComponent: () => import('./features/request/request-session').then((m) => m.RequestSession),
    title: 'طلب جلسة — Dose',
  },
  {
    path: 'admin',
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.adminRoutes),
  },
  { path: '**', redirectTo: '' },
];
