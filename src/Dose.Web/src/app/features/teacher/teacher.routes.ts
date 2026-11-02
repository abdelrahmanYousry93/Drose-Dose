import { Routes } from '@angular/router';
import { TeacherAvailability, TeacherSessionPage, TeacherSessions, TeacherShell, teacherGuard } from './teacher';

export const teacherRoutes: Routes = [
  {
    path: '',
    component: TeacherShell,
    canActivate: [teacherGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'sessions' },
      { path: 'sessions', component: TeacherSessions, title: 'titles.teacherSessions' },
      { path: 'sessions/:id', component: TeacherSessionPage, title: 'titles.teacherSession' },
      { path: 'availability', component: TeacherAvailability, title: 'titles.teacherAvailability' },
      {
        path: 'proposals', title: 'proposals.title',
        loadComponent: () => import('./teacher-proposals').then((m) => m.TeacherProposals),
      },
    ],
  },
];
