import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AuditPageComponent } from './audit.page';

const routes: Routes = [
  { path: '', component: AuditPageComponent }
];

@NgModule({
  declarations: [AuditPageComponent],
  imports: [CommonModule, RouterModule.forChild(routes)]
})
export class AuditModule {}

