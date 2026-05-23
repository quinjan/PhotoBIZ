import { Component, Input } from '@angular/core';
import { AgGridAngular } from 'ag-grid-angular';
import {
  ClientSideRowModelModule,
  ColDef,
  DateFilterModule,
  GetRowIdParams,
  GridOptions,
  ModuleRegistry,
  NumberFilterModule,
  PaginationModule,
  QuickFilterModule,
  TextFilterModule,
  ValidationModule,
} from 'ag-grid-community';

ModuleRegistry.registerModules([
  ClientSideRowModelModule,
  DateFilterModule,
  NumberFilterModule,
  PaginationModule,
  QuickFilterModule,
  TextFilterModule,
  ValidationModule,
]);

@Component({
  selector: 'admin-grid',
  standalone: true,
  imports: [AgGridAngular],
  template: `
    <ag-grid-angular
      class="ag-theme-quartz admin-grid"
      [rowData]="rows"
      [columnDefs]="columns"
      [defaultColDef]="defaultColDef"
      [domLayout]="'autoHeight'"
      [pagination]="pagination"
      [paginationPageSize]="paginationPageSize"
      [paginationPageSizeSelector]="paginationPageSizeSelector"
      [quickFilterText]="quickFilterText"
      [getRowId]="getRowId"
      [theme]="'legacy'"
    />
  `,
})
export class AdminGridComponent<T extends { readonly id: string }> {
  @Input({ required: true }) rowData: readonly T[] = [];
  @Input({ required: true }) columnDefs: readonly ColDef<T>[] = [];
  @Input() quickFilterText = '';
  @Input() pagination = true;
  @Input() paginationPageSize = 10;
  @Input() paginationPageSizeSelector: number[] | false = [10, 20, 50];

  readonly defaultColDef: ColDef<T> = {
    filter: true,
    flex: 1,
    minWidth: 130,
    resizable: true,
    sortable: true,
  };

  readonly getRowId: GridOptions<T>['getRowId'] = (params: GetRowIdParams<T>) => params.data.id;

  protected get rows(): T[] {
    return [...this.rowData];
  }

  protected get columns(): ColDef<T>[] {
    return [...this.columnDefs];
  }
}
