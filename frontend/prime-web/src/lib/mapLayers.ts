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
import { fetchParcelsInExtent, fetchReferenceLayer } from '../api/gis';
import type { ReferenceLayerName } from './types';

/**
 * Map layers shared by the Tax Map workspace and the printable tax map
 * (docs/GIS.md §5) — one definition of how each layer loads and looks.
 */

export const MAP_PROJECTION = 'EPSG:3857';
export const DATA_PROJECTION = 'EPSG:4326';

export type MapLayerName = 'parcels' | ReferenceLayerName;

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
};

export const LAYER_ORDER: MapLayerName[] = ['zones', 'barangays', 'roads', 'parcels'];

export const highlightStyle = new Style({
  stroke: new Stroke({ color: '#ea580c', width: 3 }),
  fill: new Fill({ color: 'rgba(234, 88, 12, 0.18)' }),
});

// Labels only when zoomed in far enough to be legible (~zoom 14+).
const LABEL_MAX_RESOLUTION = 10;

function layerStyle(name: MapLayerName): Style | ((feature: FeatureLike, resolution: number) => Style) {
  const def = LAYERS[name];
  const base = new Style({
    stroke: new Stroke({ color: def.stroke, width: def.width, lineDash: def.lineDash }),
    fill: def.fill ? new Fill({ color: def.fill }) : undefined,
  });
  if (name === 'parcels') {
    return base;
  }

  return (feature, resolution) => {
    const label = name === 'zones' ? (feature.get('key') as string) : (feature.get('name') as string | null);
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
export function createDataLayers(getAsOf: () => string, onStatus: (name: MapLayerName, status: LayerStatus) => void) {
  const reference = (name: ReferenceLayerName) =>
    createExtentLoadedLayer(name, (bbox) => fetchReferenceLayer(name, bbox, getAsOf()), onStatus);

  return {
    parcels: createExtentLoadedLayer('parcels', fetchParcelsInExtent, onStatus),
    zones: reference('zones'),
    barangays: reference('barangays'),
    roads: reference('roads'),
  } satisfies Record<MapLayerName, VectorLayer>;
}

export const todayIso = () => new Date().toISOString().slice(0, 10);
