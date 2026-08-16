import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { DashboardPageComponent } from './dashboard.page';

const routes: Routes = [
  { path: '', component: DashboardPageComponent }
];

@NgModule({
  imports: [CommonModule, RouterModule.forChild(routes), DashboardPageComponent]
})
export class DashboardModule {}

