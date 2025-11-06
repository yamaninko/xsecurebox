import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { LoginPageComponent } from './login.page';

const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginPageComponent }
];

@NgModule({
  imports: [CommonModule, RouterModule.forChild(routes), LoginPageComponent]
})
export class AuthModule {}

