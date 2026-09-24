/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_SUPABASE_URL: string;
  readonly VITE_SUPABASE_ANON_KEY: string;
  readonly VITE_API_BASE_URL: string;
  /** Optional basemap XYZ tile URL template; defaults to OpenStreetMap (dev only). */
  readonly VITE_MAP_TILE_URL?: string;
  readonly VITE_MAP_TILE_ATTRIBUTION?: string;
  /** Optional initial map center "lon,lat" (WGS84) and zoom. */
  readonly VITE_MAP_INITIAL_CENTER?: string;
  readonly VITE_MAP_INITIAL_ZOOM?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
