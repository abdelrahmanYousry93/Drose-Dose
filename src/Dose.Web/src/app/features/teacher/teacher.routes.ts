import { Routes } from '@angular/router';
import { TeacherAvailability, TeacherSessionPage, TeacherSessions, TeacherShell, teacherGuard } from './teacher';

export const teacherRoutes: Routes = [
  {
    path: '',
    component: TeacherShell,
    canActivate: [teacherGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'sessions' },
      { path: 'sessions', component: TeacherSessions, title: 'جلساتي — Dose' },
      { path: 'sessions/:id', component: TeacherSessionPage, title: 'الجلسة — Dose' },
      { path: 'availability', component: TeacherAvailability, title: 'مواعيدي — Dose' },
    ],
  },
];
