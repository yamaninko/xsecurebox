import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { LoginPageComponent } from './login.page';
import { ChangePasswordPageComponent } from './change-password.page';
import { MfaSetupPageComponent } from './mfa-setup.page';

const routes: Routes = [
  { path: '', redirectTo: 'login', pathMatch: 'full' },
  { path: 'login', component: LoginPageComponent },
  { path: 'change-password', component: ChangePasswordPageComponent },
  { path: 'mfa-setup', component: MfaSetupPageComponent }
];

@NgModule({
  imports: [CommonModule, RouterModule.forChild(routes), LoginPageComponent, ChangePasswordPageComponent, MfaSetupPageComponent]
})
export class AuthModule {}

