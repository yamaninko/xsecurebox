import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, ConfirmDialogData } from '../../../core/services/notification.service';
import { Subscription } from 'rxjs';

interface ConfirmDialog extends ConfirmDialogData {
  callback: (result: boolean) => void;
  show: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="confirm-overlay" *ngIf="dialog?.show" (click)="cancel()">
      <div class="confirm-dialog" 
           [class.dialog-danger]="dialog?.type === 'danger'"
           [class.dialog-warning]="dialog?.type === 'warning'"
           [class.dialog-info]="dialog?.type === 'info'"
           (click)="$event.stopPropagation()">
        <div class="dialog-icon">
          <span *ngIf="dialog?.type === 'danger'">⚠️</span>
          <span *ngIf="dialog?.type === 'warning'">⚡</span>
          <span *ngIf="dialog?.type === 'info'">💡</span>
        </div>
        
        <div class="dialog-content">
          <h3 class="dialog-title">{{ dialog?.title }}</h3>
          <p class="dialog-message">{{ dialog?.message }}</p>
        </div>

        <div class="dialog-actions">
          <button class="btn btn-cancel" (click)="cancel()">
            {{ dialog?.cancelText }}
          </button>
          <button class="btn btn-confirm" 
                  [class.btn-danger]="dialog?.type === 'danger'"
                  [class.btn-warning]="dialog?.type === 'warning'"
                  [class.btn-info]="dialog?.type === 'info'"
                  (click)="confirm()">
            {{ dialog?.confirmText }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .confirm-overlay {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(0, 0, 0, 0.6);
      backdrop-filter: blur(4px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 10000;
      animation: fadeIn 0.2s ease-out;
    }

    @keyframes fadeIn {
      from {
        opacity: 0;
      }
      to {
        opacity: 1;
      }
    }

    .confirm-dialog {
      background: white;
      border-radius: 16px;
      max-width: 480px;
      width: 90%;
      padding: 32px;
      box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
      animation: slideUp 0.3s ease-out;
      text-align: center;
    }

    @keyframes slideUp {
      from {
        transform: translateY(30px);
        opacity: 0;
      }
      to {
        transform: translateY(0);
        opacity: 1;
      }
    }

    .dialog-icon {
      font-size: 64px;
      margin-bottom: 20px;
      animation: bounceIn 0.5s ease-out;
    }

    @keyframes bounceIn {
      0% {
        transform: scale(0);
      }
      50% {
        transform: scale(1.1);
      }
      100% {
        transform: scale(1);
      }
    }

    .dialog-content {
      margin-bottom: 28px;
    }

    .dialog-title {
      font-size: 24px;
      font-weight: 700;
      color: #2d3748;
      margin: 0 0 12px 0;
    }

    .dialog-message {
      font-size: 16px;
      color: #718096;
      line-height: 1.6;
      margin: 0;
    }

    .dialog-actions {
      display: flex;
      gap: 12px;
      justify-content: center;
    }

    .btn {
      padding: 12px 32px;
      border-radius: 10px;
      font-size: 15px;
      font-weight: 600;
      cursor: pointer;
      border: none;
      transition: all 0.2s;
      min-width: 120px;
    }

    .btn:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    .btn:active {
      transform: translateY(0);
    }

    .btn-cancel {
      background: #e2e8f0;
      color: #4a5568;
    }

    .btn-cancel:hover {
      background: #cbd5e0;
    }

    .btn-confirm {
      color: white;
    }

    .btn-danger {
      background: linear-gradient(135deg, #fc8181 0%, #f56565 100%);
    }

    .btn-danger:hover {
      background: linear-gradient(135deg, #f56565 0%, #e53e3e 100%);
    }

    .btn-warning {
      background: linear-gradient(135deg, #f6ad55 0%, #ed8936 100%);
    }

    .btn-warning:hover {
      background: linear-gradient(135deg, #ed8936 0%, #dd6b20 100%);
    }

    .btn-info {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    }

    .btn-info:hover {
      background: linear-gradient(135deg, #5568d3 0%, #653a8a 100%);
    }

    @media (max-width: 768px) {
      .confirm-dialog {
        padding: 24px;
      }

      .dialog-icon {
        font-size: 48px;
      }

      .dialog-title {
        font-size: 20px;
      }

      .dialog-message {
        font-size: 14px;
      }

      .dialog-actions {
        flex-direction: column-reverse;
      }

      .btn {
        width: 100%;
      }
    }
  `]
})
export class ConfirmDialogComponent implements OnInit, OnDestroy {
  dialog: ConfirmDialog | null = null;
  private subscription?: Subscription;

  constructor(private notificationService: NotificationService) {}

  ngOnInit() {
    this.subscription = this.notificationService.confirm$.subscribe(data => {
      this.dialog = {
        ...data,
        show: true
      };
    });
  }

  ngOnDestroy() {
    if (this.subscription) {
      this.subscription.unsubscribe();
    }
  }

  confirm() {
    if (this.dialog) {
      this.dialog.callback(true);
      this.dialog.show = false;
      setTimeout(() => this.dialog = null, 300);
    }
  }

  cancel() {
    if (this.dialog) {
      this.dialog.callback(false);
      this.dialog.show = false;
      setTimeout(() => this.dialog = null, 300);
    }
  }
}

