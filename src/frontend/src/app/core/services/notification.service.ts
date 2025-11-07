import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

export interface ToastMessage {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message: string;
  duration?: number;
}

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  type?: 'danger' | 'warning' | 'info';
}

@Injectable({
  providedIn: 'root'
})
export class NotificationService {
  private toastSubject = new Subject<ToastMessage>();
  private confirmSubject = new Subject<ConfirmDialogData & { callback: (result: boolean) => void }>();

  toast$ = this.toastSubject.asObservable();
  confirm$ = this.confirmSubject.asObservable();

  constructor() {}

  // Toast Notifications
  success(title: string, message: string, duration = 3000) {
    this.showToast('success', title, message, duration);
  }

  error(title: string, message: string, duration = 5000) {
    this.showToast('error', title, message, duration);
  }

  warning(title: string, message: string, duration = 4000) {
    this.showToast('warning', title, message, duration);
  }

  info(title: string, message: string, duration = 3000) {
    this.showToast('info', title, message, duration);
  }

  private showToast(type: ToastMessage['type'], title: string, message: string, duration: number) {
    const toast: ToastMessage = {
      id: this.generateId(),
      type,
      title,
      message,
      duration
    };
    this.toastSubject.next(toast);
  }

  // Confirmation Dialog
  confirm(data: ConfirmDialogData): Promise<boolean> {
    return new Promise((resolve) => {
      this.confirmSubject.next({
        ...data,
        confirmText: data.confirmText || 'Onayla',
        cancelText: data.cancelText || 'İptal',
        type: data.type || 'info',
        callback: resolve
      });
    });
  }

  private generateId(): string {
    return `toast-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
  }
}

