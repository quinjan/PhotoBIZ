type PhotoBizBrowserConfig = {
  readonly apiBaseUrl?: string;
};

declare global {
  interface Window {
    readonly photoBizConfig?: PhotoBizBrowserConfig;
  }
}

export function resolvePhotoBizApiBaseUrl(): string {
  const configuredBaseUrl = window.photoBizConfig?.apiBaseUrl?.trim();
  if (configuredBaseUrl) {
    return trimTrailingSlash(configuredBaseUrl);
  }

  const { protocol, hostname, port } = window.location;
  if (hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '') {
    return 'http://localhost:5082';
  }

  if (hostname.startsWith('admin.')) {
    return `${protocol}//api.${hostname.slice('admin.'.length)}${port ? `:${port}` : ''}`;
  }

  if (hostname.startsWith('booth.')) {
    return `${protocol}//api.${hostname.slice('booth.'.length)}${port ? `:${port}` : ''}`;
  }

  return `${protocol}//${hostname}${port ? `:${port}` : ''}`;
}

function trimTrailingSlash(value: string): string {
  return value.endsWith('/') ? value.slice(0, -1) : value;
}
