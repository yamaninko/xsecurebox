import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ChainService } from '../../core/services/chain.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-chain-page',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chain.page.html',
  styleUrls: ['./chain.page.css']
})
export class ChainPageComponent implements OnInit, OnDestroy {
  loading = true;
  saving = false;
  data: any = null;
  form = {
    rpcUrlsText: '',
    quorum: 1,
    systemName: 'xsecurebox',
    requireOnRetrieve: true,
    contractAddress: '',
    paused: false,
    newOwner: '',
    nodeCount: 1
  };

  constructor(
    private chain: ChainService,
    private notify: NotificationService
  ) {}

  private timer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.refresh();
    this.timer = setInterval(() => this.refresh(), 8000);
  }

  ngOnDestroy(): void {
    if (this.timer) {
      clearInterval(this.timer);
    }
  }

  refresh(): void {
    this.loading = true;
    this.chain.getDashboard().subscribe({
      next: (res) => {
        this.data = res.data;
        this.syncForm();
        this.loading = false;
      },
      error: (err) => {
        this.loading = false;
        this.notify.error('Hata', err.error?.error?.message || 'ETH durumu alınamadı');
      }
    });
  }

  save(): void {
    this.saving = true;
    const afterSave = () => {
      const desired = Number(this.form.nodeCount);
      const running = this.data?.runningNodeCount || this.data?.nodes?.length || 0;
      if (desired && desired !== running) {
        this.chain.scale(desired).subscribe({
          next: (res) => {
            this.data = res.data;
            this.syncForm();
            this.saving = false;
            this.notify.success('Kaydedildi', `${this.data.runningNodeCount} ETH VM + load balancer`);
          },
          error: (err) => {
            this.saving = false;
            this.notify.error('Hata', err.error?.error?.message || 'VM kümesi başlatılamadı');
          }
        });
        return;
      }
      this.saving = false;
      this.notify.success('Kaydedildi', 'ETH parametreleri güncellendi');
    };
    this.chain.updateSettings({
      rpcUrlsText: this.form.rpcUrlsText,
      quorum: Number(this.form.quorum),
      systemName: this.form.systemName,
      requireOnRetrieve: this.form.requireOnRetrieve,
      contractAddress: this.form.contractAddress || null,
      paused: this.form.paused,
      newOwner: this.form.newOwner || null
    }).subscribe({
      next: (res) => {
        this.data = res.data;
        this.syncForm();
        afterSave();
      },
      error: (err) => {
        this.saving = false;
        this.notify.error('Hata', err.error?.error?.message || 'Kaydedilemedi');
      }
    });
  }

  scale(): void {
    const n = Number(this.form.nodeCount);
    if (n < 1 || n > (this.data?.maxNodeCount || 7)) {
      this.notify.error('Geçersiz', 'VM sayısı 1-7 arası olmalı');
      return;
    }
    this.saving = true;
    this.chain.scale(n).subscribe({
      next: (res) => {
        this.data = res.data;
        this.syncForm();
        this.saving = false;
        this.notify.success('ETH kümesi', `${this.data.runningNodeCount} VM çalışıyor ve birbirine bağlı`);
      },
      error: (err) => {
        this.saving = false;
        this.notify.error('Hata', err.error?.error?.message || 'VM kümesi başlatılamadı');
      }
    });
  }

  redeploy(): void {
    if (!confirm('Yeni kontrat yayınlansın mı? Eski mühürler eski adreste kalır.')) {
      return;
    }
    this.saving = true;
    this.chain.redeploy(this.form.systemName).subscribe({
      next: (res) => {
        this.data = res.data;
        this.syncForm();
        this.saving = false;
        this.notify.success('Yayınlandı', this.data?.contractAddress);
      },
      error: (err) => {
        this.saving = false;
        this.notify.error('Hata', err.error?.error?.message || 'Yayın başarısız');
      }
    });
  }

  private syncForm(): void {
    if (!this.data) {
      return;
    }
    this.form.rpcUrlsText = this.data.rpcUrlsText || '';
    this.form.quorum = this.data.quorum || 1;
    this.form.systemName = this.data.systemName || 'xsecurebox';
    this.form.requireOnRetrieve = this.data.requireOnRetrieve !== false;
    this.form.contractAddress = this.data.contractAddress || '';
    this.form.paused = !!this.data.paused;
    this.form.newOwner = '';
    this.form.nodeCount = this.data.runningNodeCount || this.data.nodes?.length || 1;
  }
}
