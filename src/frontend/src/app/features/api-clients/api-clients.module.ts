import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { ApiClientsPageComponent } from './api-clients.page';

const routes: Routes = [
  { path: '', component: ApiClientsPageComponent }
];

@NgModule({
  imports: [RouterModule.forChild(routes), ApiClientsPageComponent],
  exports: [RouterModule]
})
export class ApiClientsModule { }

