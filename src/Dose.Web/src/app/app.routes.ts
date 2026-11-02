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
    // Book a private session or a private group of friends from the teachers' free times.
    path: 'private',
    loadComponent: () => import('./features/private/private-booking').then((m) => m.PrivateBooking),
    title: 'titles.private',
  },
  {
    // Join a friend's private group with the invite code: /join/<code> (or /join to type it).
    path: 'join/:code',
    loadComponent: () => import('./features/private/join-group').then((m) => m.JoinGroup),
    title: 'titles.join',
  },
  { path: 'join', redirectTo: '' },
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
