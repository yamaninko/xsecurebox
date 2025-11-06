import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RolesPageComponent } from './roles.page';

const routes: Routes = [
  { path: '', component: RolesPageComponent }
];

@NgModule({
  imports: [CommonModule, RouterModule.forChild(routes), RolesPageComponent]
})
export class RolesModule {}

