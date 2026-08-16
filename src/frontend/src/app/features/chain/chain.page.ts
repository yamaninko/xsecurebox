import { Component, OnInit } from '@angular/core';
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
export class ChainPageComponent implements OnInit {
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
    newOwner: ''
  };

  constructor(
    private chain: ChainService,
    private notify: NotificationService
  ) {}

  ngOnInit(): void {
    this.refresh();
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
        this.saving = false;
        this.notify.success('Kaydedildi', 'ETH parametreleri güncellendi');
      },
      error: (err) => {
        this.saving = false;
        this.notify.error('Hata', err.error?.error?.message || 'Kaydedilemedi');
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
  }
}
