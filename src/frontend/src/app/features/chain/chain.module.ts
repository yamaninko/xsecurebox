import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CommonModule } from '@angular/common';
import { ChainPageComponent } from './chain.page';

const routes: Routes = [
  { path: '', component: ChainPageComponent }
];

@NgModule({
  imports: [CommonModule, RouterModule.forChild(routes), ChainPageComponent]
})
export class ChainModule {}
