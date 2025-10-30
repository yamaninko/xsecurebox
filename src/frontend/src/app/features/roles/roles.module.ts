import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RolesPageComponent } from './roles.page';

const routes: Routes = [
  { path: '', component: RolesPageComponent }
];

@NgModule({
  declarations: [RolesPageComponent],
  imports: [CommonModule, RouterModule.forChild(routes)]
})
export class RolesModule {}

