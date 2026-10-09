import type Feature from 'ol/Feature';
import type { FeatureLike } from 'ol/Feature';
import TileLayer from 'ol/layer/Tile';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import XYZ from 'ol/source/XYZ';
import GeoJSON from 'ol/format/GeoJSON';
import { bbox as bboxStrategy } from 'ol/loadingstrategy';
import { transformExtent } from 'ol/proj';
import { Fill, Stroke, Style, Text } from 'ol/style';
import { mapConfig } from './mapConfig';
import { fetchParcelsInExtent, fetchReferenceLayer, fetchValueMap } from '../api/gis';
import type { ReferenceLayerName } from './types';

/**
 * Map layers shared by the Tax Map workspace and the printable tax map
 * (docs/GIS.md §5) — one definition of how each layer loads and looks.
 */

export const MAP_PROJECTION = 'EPSG:3857';
export const DATA_PROJECTION = 'EPSG:4326';

export type MapLayerName = 'parcels' | 'values' | ReferenceLayerName;

export interface LayerStatus {
  truncated: boolean;
  error: string | null;
}

interface LayerDefinition {
  title: string;
  /** Shown (and fetched) only at or above this zoom — a UI/performance setting. */
  minZoom: number;
  stroke: string;
  fill?: string;
  lineDash?: number[];
  width: number;
}

export const LAYERS: Record<MapLayerName, LayerDefinition> = {
  parcels: { title: 'Parcels', minZoom: mapConfig.parcelMinZoom, stroke: '#1d4ed8', fill: 'rgba(29, 78, 216, 0.08)', width: 1.5 },
  zones: { title: 'Valuation zones', minZoom: 12, stroke: '#047857', fill: 'rgba(4, 120, 87, 0.06)', width: 1.5 },
  barangays: { title: 'Barangay boundaries', minZoom: 11, stroke: '#7c3aed', lineDash: [8, 4], width: 2 },
  roads: { title: 'Roads', minZoom: 13, stroke: '#b45309', width: 2.5 },
  sections: { title: 'Tax map sections', minZoom: 13, stroke: '#be123c', lineDash: [12, 4, 2, 4], width: 2 },
  values: { title: 'Land values', minZoom: mapConfig.parcelMinZoom, stroke: '#374151', fill: 'rgba(37, 99, 235, 0.45)', width: 0.5 },
  submarketareas: { title: 'Sub-market areas', minZoom: 12, stroke: '#0e7490', fill: 'rgba(14, 116, 144, 0.05)', lineDash: [4, 4], width: 2 },
};

export const LAYER_ORDER: MapLayerName[] = ['zones', 'barangays', 'submarketareas', 'sections', 'roads', 'values', 'parcels'];

/** How the land value map is drawn: under which SMV (null: the approved SMV in force) and coloured by what. */
export interface ValueMapOptions {
  smvId: string | null;
  colorBy: 'subClass' | 'value';
  /** Upper bounds of the value bands but the last, ascending (colorBy 'value'). */
  bands: number[];
}

// Categorical colours for sub-classes, and a light-to-dark sequence for value bands.
const CATEGORY_COLORS = ['#2563eb', '#16a34a', '#d97706', '#9333ea', '#dc2626', '#0891b2', '#65a30d', '#db2777', '#4f46e5', '#ca8a04'];
const BAND_COLORS = ['#fef3c7', '#fcd34d', '#f59e0b', '#d97706', '#92400e'];
export const NO_VALUE_COLOR = '#9ca3af';

const hash = (s: string) => [...s].reduce((h, c) => (h * 31 + c.charCodeAt(0)) >>> 0, 7);

/** The colour of one value-map feature: its sub-class's, its value band's, or grey when it has no value. */
export function valueColor(props: { classification: string | null; subClass: string | null; unitValue: number | null }, options: ValueMapOptions): string {
  if (props.unitValue == null) {
    return NO_VALUE_COLOR;
  }
  if (options.colorBy === 'subClass') {
    return CATEGORY_COLORS[hash(`${props.classification}|${props.subClass ?? ''}`) % CATEGORY_COLORS.length];
  }
  const band = options.bands.findIndex((upper) => props.unitValue! <= upper);
  return BAND_COLORS[band === -1 ? options.bands.length : band] ?? BAND_COLORS[BAND_COLORS.length - 1];
}

/** Up to five quantile bands of the values loaded, as upper bounds but the last. */
export function valueBands(values: number[]): number[] {
  const sorted = [...new Set(values)].sort((a, b) => a - b);
  if (sorted.length <= 1) {
    return [];
  }
  const count = Math.min(5, sorted.length);
  const bounds = Array.from({ length: count - 1 }, (_, i) => sorted[Math.floor(((i + 1) * sorted.length) / count) - 1]);
  return [...new Set(bounds)];
}

export interface ValueLegendEntry { color: string; label: string; parcels: number }

/** The legend of the loaded value-map features. */
export function valueLegend(features: Feature[], options: ValueMapOptions): ValueLegendEntry[] {
  const fmt = (n: number) => n.toLocaleString('en-PH', { maximumFractionDigits: 2 });
  const entries = new Map<string, ValueLegendEntry & { order: number }>();
  for (const f of features) {
    const p = f.getProperties() as { classification: string | null; subClass: string | null; unitValue: number | null; unit: string | null };
    const color = valueColor(p, options);
    let label: string;
    let order: number;
    if (p.unitValue == null) {
      label = 'No unit value';
      order = Number.MAX_SAFE_INTEGER;
    } else if (options.colorBy === 'subClass') {
      label = `${p.classification ?? ''}${p.subClass ? ` — ${p.subClass}` : ''}: ${fmt(p.unitValue)} ${p.unit ?? ''}`.trim();
      order = -p.unitValue;
    } else {
      const band = options.bands.findIndex((upper) => p.unitValue! <= upper);
      const i = band === -1 ? options.bands.length : band;
      const low = i === 0 ? null : options.bands[i - 1];
      const high = i < options.bands.length ? options.bands[i] : null;
      label = low == null && high == null ? `${fmt(p.unitValue)}` : low == null ? `up to ${fmt(high!)}` : high == null ? `over ${fmt(low)}` : `over ${fmt(low)} to ${fmt(high)}`;
      order = i;
    }
    const key = `${color}|${label}`;
    const entry = entries.get(key);
    if (entry) {
      entry.parcels += 1;
    } else {
      entries.set(key, { color, label, parcels: 1, order });
    }
  }
  return [...entries.values()].sort((a, b) => a.order - b.order).map(({ color, label, parcels }) => ({ color, label, parcels }));
}

export const highlightStyle = new Style({
  stroke: new Stroke({ color: '#ea580c', width: 3 }),
  fill: new Fill({ color: 'rgba(234, 88, 12, 0.18)' }),
});

// Labels only when zoomed in far enough to be legible (~zoom 14+; parcel numbers ~zoom 17+).
const LABEL_MAX_RESOLUTION = 10;
const PARCEL_LABEL_MAX_RESOLUTION = 1.2;

function layerStyle(name: MapLayerName): Style | ((feature: FeatureLike, resolution: number) => Style) {
  const def = LAYERS[name];
  const base = new Style({
    stroke: new Stroke({ color: def.stroke, width: def.width, lineDash: def.lineDash }),
    fill: def.fill ? new Fill({ color: def.fill }) : undefined,
  });
  if (name === 'parcels') {
    // The assessor's parcel number within its section (MRPAAO Ch. II §1), once placed.
    return (feature, resolution) => {
      const parcelNumber = feature.get('parcelNumber') as number | null;
      if (parcelNumber == null || resolution > PARCEL_LABEL_MAX_RESOLUTION) {
        return base;
      }
      return new Style({
        stroke: base.getStroke() ?? undefined,
        fill: base.getFill() ?? undefined,
        text: new Text({
          text: String(parcelNumber).padStart(2, '0'),
          font: '600 12px system-ui, sans-serif',
          fill: new Fill({ color: def.stroke }),
          stroke: new Stroke({ color: '#ffffff', width: 3 }),
          overflow: false,
        }),
      });
    };
  }

  return (feature, resolution) => {
    const label = name === 'zones' || name === 'submarketareas' ? ((feature.get('name') as string | null) ?? (feature.get('key') as string)) : (feature.get('name') as string | null);
    if (!label || resolution > LABEL_MAX_RESOLUTION) {
      return base;
    }
    return new Style({
      stroke: base.getStroke() ?? undefined,
      fill: base.getFill() ?? undefined,
      text: new Text({
        text: label,
        font: '600 12px system-ui, sans-serif',
        fill: new Fill({ color: def.stroke }),
        stroke: new Stroke({ color: '#ffffff', width: 3 }),
        placement: name === 'roads' ? 'line' : 'point',
        overflow: name !== 'roads',
      }),
    });
  };
}

export function createBaseLayer() {
  return new TileLayer({
    source: new XYZ({ url: mapConfig.tileUrl, attributions: mapConfig.tileAttribution, maxZoom: 19, crossOrigin: 'anonymous' }),
  });
}

const clamp = (n: number, min: number, max: number) => Math.min(max, Math.max(min, n));

type Bbox = [number, number, number, number];

/**
 * A vector layer loaded per visible extent from the API. A truncated or
 * failed response is not remembered as loaded, so the area is re-fetched
 * after zooming in or on the next pan.
 */
function createExtentLoadedLayer(
  name: MapLayerName,
  fetchCollection: (bbox: Bbox) => Promise<{ truncated: boolean }>,
  onStatus: (name: MapLayerName, status: LayerStatus) => void,
) {
  const geojson = new GeoJSON();
  const source: VectorSource = new VectorSource({
    strategy: bboxStrategy,
    loader: (extent, _resolution, projection, success, failure) => {
      const [minLon, minLat, maxLon, maxLat] = transformExtent(extent, projection, DATA_PROJECTION);
      const bbox: Bbox = [clamp(minLon, -180, 180), clamp(minLat, -90, 90), clamp(maxLon, -180, 180), clamp(maxLat, -90, 90)];
      if (bbox[0] >= bbox[2] || bbox[1] >= bbox[3]) {
        success?.([]);
        return;
      }

      fetchCollection(bbox)
        .then((collection) => {
          const features = geojson.readFeatures(collection, {
            dataProjection: DATA_PROJECTION,
            featureProjection: projection,
          }) as Feature[];
          source.addFeatures(features);
          if (collection.truncated) {
            source.removeLoadedExtent(extent);
          }
          onStatus(name, { truncated: collection.truncated, error: null });
          success?.(features);
        })
        .catch((error: Error) => {
          source.removeLoadedExtent(extent);
          onStatus(name, { truncated: false, error: error.message });
          failure?.();
        });
    },
  });

  return new VectorLayer({
    source,
    style: layerStyle(name),
    // OpenLayers' layer minZoom is exclusive, hence the small offset.
    minZoom: LAYERS[name].minZoom - 0.001,
    zIndex: LAYER_ORDER.indexOf(name),
    // Drop overlapping labels instead of drawing them on top of each other.
    declutter: name !== 'parcels',
    properties: { name },
  });
}

/**
 * All data layers, keyed by name. Reference layers read the as-of date
 * through `getAsOf` at load time; after changing it, call
 * `layer.getSource()?.refresh()` to reload.
 */
export function createDataLayers(
  getAsOf: () => string,
  onStatus: (name: MapLayerName, status: LayerStatus) => void,
  getValueOptions: () => ValueMapOptions = () => ({ smvId: null, colorBy: 'subClass', bands: [] }),
) {
  const reference = (name: ReferenceLayerName) =>
    createExtentLoadedLayer(name, (bbox) => fetchReferenceLayer(name, bbox, getAsOf()), onStatus);
  const values = createExtentLoadedLayer('values', (bbox) => fetchValueMap(bbox, getValueOptions().smvId, getAsOf()), onStatus);
  // The land value map: each parcel filled by its sub-class or value band, labelled with both when zoomed in.
  values.setStyle((feature, resolution) => {
    const p = feature.getProperties() as { classification: string | null; subClass: string | null; unitValue: number | null };
    const color = valueColor(p, getValueOptions());
    const label = p.unitValue == null || resolution > PARCEL_LABEL_MAX_RESOLUTION * 2 ? undefined
      : `${p.subClass ?? ''}\n${p.unitValue.toLocaleString('en-PH', { maximumFractionDigits: 0 })}`.trim();
    return new Style({
      stroke: new Stroke({ color: '#374151', width: 0.5 }),
      fill: new Fill({ color: `${color}b3` }),
      // Below the parcel number the parcels layer draws at the centre.
      text: label ? new Text({
        text: label, font: '600 11px system-ui, sans-serif', offsetY: 22, fill: new Fill({ color: '#111827' }), stroke: new Stroke({ color: '#ffffff', width: 3 }),
      }) : undefined,
    });
  });

  return {
    parcels: createExtentLoadedLayer('parcels', fetchParcelsInExtent, onStatus),
    values,
    zones: reference('zones'),
    barangays: reference('barangays'),
    roads: reference('roads'),
    sections: reference('sections'),
    submarketareas: reference('submarketareas'),
  } satisfies Record<MapLayerName, VectorLayer>;
}

/** Today's date where the user is (the office's day), not UTC's: before 08:00 in Manila UTC is still on yesterday. */
export const todayIso = () => {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
};
