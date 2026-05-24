import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  {
    path: 'dashboard',
    loadComponent: () => import('./admin-pages').then((pages) => pages.DashboardPageComponent),
  },
  {
    path: 'subscriptions',
    loadComponent: () => import('./admin-pages').then((pages) => pages.SubscriptionsPageComponent),
  },
  {
    path: 'subscriptions/detail',
    loadComponent: () =>
      import('./admin-pages').then((pages) => pages.SubscriptionDetailPageComponent),
  },
  {
    path: 'clients',
    loadComponent: () => import('./admin-pages').then((pages) => pages.ClientsPageComponent),
  },
  {
    path: 'clients/detail',
    loadComponent: () => import('./admin-pages').then((pages) => pages.ClientDetailPageComponent),
  },
  {
    path: 'users',
    loadComponent: () => import('./admin-pages').then((pages) => pages.UsersPageComponent),
  },
  {
    path: 'users/detail',
    loadComponent: () => import('./admin-pages').then((pages) => pages.UserDetailPageComponent),
  },
  {
    path: 'locations',
    loadComponent: () => import('./admin-pages').then((pages) => pages.LocationsPageComponent),
  },
  {
    path: 'booths',
    loadComponent: () => import('./admin-pages').then((pages) => pages.BoothsPageComponent),
  },
  {
    path: 'booths/detail',
    loadComponent: () => import('./admin-pages').then((pages) => pages.BoothDetailPageComponent),
  },
  {
    path: 'packages',
    loadComponent: () => import('./admin-pages').then((pages) => pages.PackagesPageComponent),
  },
  {
    path: 'packages/detail',
    loadComponent: () => import('./admin-pages').then((pages) => pages.PackageDetailPageComponent),
  },
  {
    path: 'transactions',
    loadComponent: () => import('./admin-pages').then((pages) => pages.TransactionsPageComponent),
  },
  {
    path: 'pos',
    loadComponent: () => import('./admin-pages').then((pages) => pages.PosPageComponent),
  },
  {
    path: 'reports',
    loadComponent: () => import('./admin-pages').then((pages) => pages.ReportsPageComponent),
  },
  {
    path: 'settings',
    loadComponent: () => import('./admin-pages').then((pages) => pages.SettingsPageComponent),
  },
  {
    path: 'settings/paymongo',
    loadComponent: () =>
      import('./admin-pages').then((pages) => pages.PayMongoSettingsPageComponent),
  },
  {
    path: 'account',
    loadComponent: () => import('./admin-pages').then((pages) => pages.AccountPageComponent),
  },
  {
    path: 'audit',
    loadComponent: () => import('./admin-pages').then((pages) => pages.AuditPageComponent),
  },
  { path: '**', redirectTo: 'dashboard' },
];
