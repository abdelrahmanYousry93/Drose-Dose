import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { environment } from '../../environments/environment';
import { StudentContext } from './student-context';

export interface PublicSettings {
  whatsAppBusinessNumber: string;
  businessTimeZone: string;
  fullRefundCancellationHours: number;
}

/** Loads non-sensitive settings from the API. Nothing here is hard-coded in the client. */
@Injectable({ providedIn: 'root' })
export class PublicSettingsService {
  private readonly http = inject(HttpClient);
  private readonly context = inject(StudentContext);

  readonly settings = toSignal(
    this.http.get<PublicSettings>(`${environment.apiBaseUrl}/public-settings`).pipe(
      catchError(() => of(null)),
    ),
    { initialValue: null },
  );

  /**
   * Click-to-chat link with a prefilled message (by default: the student's grade, subject, session and the step they're
   * on — see StudentContext). No phone numbers or private links are put in the URL.
   */
  whatsAppLink(message = this.context.whatsAppText()): string | null {
    const number = this.settings()?.whatsAppBusinessNumber?.replace(/\D/g, '');
    return number ? `https://wa.me/${number}?text=${encodeURIComponent(message)}` : null;
  }
}
