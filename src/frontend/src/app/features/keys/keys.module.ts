import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { KeysPageComponent } from './keys.page';

const routes: Routes = [
  { path: '', component: KeysPageComponent }
];

@NgModule({
  imports: [CommonModule, RouterModule.forChild(routes), KeysPageComponent]
})
export class KeysModule {}

