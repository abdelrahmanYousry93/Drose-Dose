import { Routes } from '@angular/router';
import { Home } from './features/home/home';

export const routes: Routes = [
  { path: '', component: Home, title: 'Dose دروس' },
  {
    path: 'open-groups',
    loadComponent: () => import('./features/coming-soon/coming-soon').then((m) => m.ComingSoon),
    data: { heading: 'الجلسات الجماعية المفتوحة' },
    title: 'جلسات جماعية مفتوحة — Dose',
  },
  {
    path: 'request-session',
    loadComponent: () => import('./features/coming-soon/coming-soon').then((m) => m.ComingSoon),
    data: { heading: 'طلب جلسة جديدة' },
    title: 'طلب جلسة — Dose',
  },
  { path: '**', redirectTo: '' },
];
