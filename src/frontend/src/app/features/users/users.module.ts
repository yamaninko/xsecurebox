import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { UsersPageComponent } from './users.page';

const routes: Routes = [
  { path: '', component: UsersPageComponent }
];

@NgModule({
  declarations: [UsersPageComponent],
  imports: [CommonModule, RouterModule.forChild(routes)]
})
export class UsersModule {}

