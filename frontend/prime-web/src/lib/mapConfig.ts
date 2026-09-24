/**
 * Basemap and initial-view settings for the GIS workspace (docs/GIS.md §5).
 *
 * The default basemap is the public OpenStreetMap tile server, which is
 * fine for development but whose usage policy does not permit heavy
 * production use — a deployment should point VITE_MAP_TILE_URL at the
 * LGU's own or a contracted tile service.
 */
const OSM_TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
const OSM_ATTRIBUTION =
  '&#169; <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">OpenStreetMap</a> contributors';

// Roughly the geographic centre of the Philippines, zoomed to show the
// whole archipelago — a neutral starting view until an LGU configures its own.
const DEFAULT_CENTER: [number, number] = [122.0, 12.5];
const DEFAULT_ZOOM = 6;

function parseLonLat(value: string | undefined): [number, number] | null {
  const parts = value?.split(',').map((p) => Number(p.trim()));
  if (!parts || parts.length !== 2 || parts.some((n) => !Number.isFinite(n))) {
    return null;
  }
  return [parts[0], parts[1]];
}

export const mapConfig = {
  tileUrl: import.meta.env.VITE_MAP_TILE_URL || OSM_TILE_URL,
  tileAttribution: import.meta.env.VITE_MAP_TILE_ATTRIBUTION || OSM_ATTRIBUTION,
  initialCenter: parseLonLat(import.meta.env.VITE_MAP_INITIAL_CENTER) ?? DEFAULT_CENTER,
  initialZoom: Number(import.meta.env.VITE_MAP_INITIAL_ZOOM) || DEFAULT_ZOOM,
  /**
   * Parcels are only requested at or above this zoom (~street level), so a
   * province- or country-wide view never asks the API for everything
   * (CLAUDE.md §71). A UI/performance setting, not a business rule.
   */
  parcelMinZoom: 14,
};
