import { Routes } from '@angular/router';
import { Home } from './features/home/home';

export const routes: Routes = [
  { path: '', component: Home, title: '' },
  {
    path: 'sessions',
    loadComponent: () => import('./features/sessions/sessions-list').then((m) => m.SessionsList),
    title: 'titles.sessions',
  },
  {
    path: 'sessions/:id',
    loadComponent: () => import('./features/sessions/session-details').then((m) => m.SessionDetailsPage),
    title: 'titles.details',
  },
  {
    path: 'open-groups',
    loadComponent: () => import('./features/sessions/sessions-list').then((m) => m.SessionsList),
    data: { fixedMode: 'Group' },
    title: 'titles.openGroups',
  },
  {
    // Secret booking link: /b/<token>. The token is the only key to the booking.
    path: 'b/:token',
    loadComponent: () => import('./features/booking/booking-page').then((m) => m.BookingPage),
    title: 'titles.booking',
  },
  {
    path: 'request-session',
    loadComponent: () => import('./features/request/request-session').then((m) => m.RequestSession),
    title: 'titles.request',
  },
  {
    path: 'login',
    loadComponent: () => import('./features/admin/admin').then((m) => m.AdminLogin),
    title: 'titles.login',
  },
  {
    path: 'teacher',
    loadChildren: () => import('./features/teacher/teacher.routes').then((m) => m.teacherRoutes),
  },
  {
    path: 'admin',
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.adminRoutes),
  },
  { path: '**', redirectTo: '' },
];
