import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { KeysPageComponent } from './keys.page';

const routes: Routes = [
  { path: '', component: KeysPageComponent }
];

@NgModule({
  declarations: [KeysPageComponent],
  imports: [CommonModule, RouterModule.forChild(routes)]
})
export class KeysModule {}

