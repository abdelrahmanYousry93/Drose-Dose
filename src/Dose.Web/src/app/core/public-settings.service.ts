import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';
import { environment } from '../../environments/environment';
import { translate } from './i18n';

export interface PublicSettings {
  whatsAppBusinessNumber: string;
  businessTimeZone: string;
  fullRefundCancellationHours: number;
}

/** Loads non-sensitive settings from the API. Nothing here is hard-coded in the client. */
@Injectable({ providedIn: 'root' })
export class PublicSettingsService {
  private readonly http = inject(HttpClient);

  readonly settings = toSignal(
    this.http.get<PublicSettings>(`${environment.apiBaseUrl}/public-settings`).pipe(
      catchError(() => of(null)),
    ),
    { initialValue: null },
  );

  /** Click-to-chat link. Only a non-sensitive message is ever put in the URL. */
  whatsAppLink(message = translate('nav.whatsappHello')): string | null {
    const number = this.settings()?.whatsAppBusinessNumber?.replace(/\D/g, '');
    return number ? `https://wa.me/${number}?text=${encodeURIComponent(message)}` : null;
  }
}
