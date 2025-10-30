import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { CertificatesPageComponent } from './certificates.page';

const routes: Routes = [
  { path: '', component: CertificatesPageComponent }
];

@NgModule({
  declarations: [CertificatesPageComponent],
  imports: [CommonModule, RouterModule.forChild(routes)]
})
export class CertificatesModule {}

