import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Card, Descriptions, Empty, Input, Spin, Tag, Typography } from 'antd';
import { EnvironmentOutlined } from '@ant-design/icons';
import 'ol/ol.css';
import OlMap from 'ol/Map';
import View from 'ol/View';
import type Feature from 'ol/Feature';
import TileLayer from 'ol/layer/Tile';
import VectorLayer from 'ol/layer/Vector';
import VectorSource from 'ol/source/Vector';
import XYZ from 'ol/source/XYZ';
import GeoJSON from 'ol/format/GeoJSON';
import WKT from 'ol/format/WKT';
import { bbox as bboxStrategy } from 'ol/loadingstrategy';
import { fromLonLat, toLonLat, transformExtent } from 'ol/proj';
import { createEmpty, extend } from 'ol/extent';
import { Fill, Stroke, Style } from 'ol/style';
import { apiGet } from '../../lib/apiClient';
import { mapConfig } from '../../lib/mapConfig';
import { fetchParcelsAtPoint, fetchParcelsInExtent, fetchPropertyParcels, fetchPropertyProfile } from '../../api/gis';
import type { PagedResult, ParcelFeatureProperties, PropertyDto } from '../../lib/types';

const MAP_PROJECTION = 'EPSG:3857';
const DATA_PROJECTION = 'EPSG:4326';

const parcelStyle = new Style({
  stroke: new Stroke({ color: '#1d4ed8', width: 1.5 }),
  fill: new Fill({ color: 'rgba(29, 78, 216, 0.08)' }),
});

const highlightStyle = new Style({
  stroke: new Stroke({ color: '#ea580c', width: 3 }),
  fill: new Fill({ color: 'rgba(234, 88, 12, 0.18)' }),
});

type Selection =
  | { origin: 'click'; parcels: ParcelFeatureProperties[] }
  | { origin: 'property'; pin: string; parcels: ParcelFeatureProperties[] };

const clamp = (n: number, min: number, max: number) => Math.min(max, Math.max(min, n));

/**
 * CLAUDE.md §38/§54 GIS workspace: parcel map, search, parcel selection →
 * Property Profile. Parcels load per visible extent from /api/gis/parcels
 * (never the whole inventory — §71) and only at street-level zoom.
 * `?propertyId=` opens the map zoomed to that property's parcels (used by
 * the Property Profile's "View on map").
 */
export function GisWorkspacePage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const mapElement = useRef<HTMLDivElement>(null);
  const mapRef = useRef<OlMap | null>(null);
  const [highlightSource] = useState(() => new VectorSource());

  const [zoom, setZoom] = useState(mapConfig.initialZoom);
  const [truncated, setTruncated] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [selection, setSelection] = useState<Selection | null>(null);
  const [searchTerm, setSearchTerm] = useState('');

  useEffect(() => {
    if (!mapElement.current) {
      return;
    }

    const geojson = new GeoJSON();
    const parcelSource = new VectorSource({
      strategy: bboxStrategy,
      loader: (extent, _resolution, projection, success, failure) => {
        const [minLon, minLat, maxLon, maxLat] = transformExtent(extent, projection, DATA_PROJECTION);
        const bbox: [number, number, number, number] = [
          clamp(minLon, -180, 180),
          clamp(minLat, -90, 90),
          clamp(maxLon, -180, 180),
          clamp(maxLat, -90, 90),
        ];
        if (bbox[0] >= bbox[2] || bbox[1] >= bbox[3]) {
          success?.([]);
          return;
        }

        fetchParcelsInExtent(bbox)
          .then((collection) => {
            const features = geojson.readFeatures(collection, {
              dataProjection: DATA_PROJECTION,
              featureProjection: projection,
            }) as Feature[];
            parcelSource.addFeatures(features);
            // An incomplete extent must not be remembered as loaded, or
            // zooming into it would never fetch the missing parcels.
            if (collection.truncated) {
              parcelSource.removeLoadedExtent(extent);
            }
            setTruncated(collection.truncated);
            setLoadError(null);
            success?.(features);
          })
          .catch((error: Error) => {
            parcelSource.removeLoadedExtent(extent);
            setLoadError(error.message);
            failure?.();
          });
      },
    });

    const map = new OlMap({
      target: mapElement.current,
      layers: [
        new TileLayer({
          source: new XYZ({ url: mapConfig.tileUrl, attributions: mapConfig.tileAttribution, maxZoom: 19 }),
        }),
        // OpenLayers' layer minZoom is exclusive, hence the small offset.
        new VectorLayer({ source: parcelSource, style: parcelStyle, minZoom: mapConfig.parcelMinZoom - 0.001 }),
        new VectorLayer({ source: highlightSource, style: highlightStyle, zIndex: 10 }),
      ],
      view: new View({
        projection: MAP_PROJECTION,
        center: fromLonLat(mapConfig.initialCenter),
        zoom: mapConfig.initialZoom,
        maxZoom: 21,
      }),
    });
    mapRef.current = map;

    map.on('moveend', () => setZoom(map.getView().getZoom() ?? 0));

    map.on('singleclick', (event) => {
      if ((map.getView().getZoom() ?? 0) < mapConfig.parcelMinZoom) {
        setNotice('Zoom in to street level to select parcels.');
        return;
      }
      const [lon, lat] = toLonLat(event.coordinate);
      setNotice(null);
      fetchParcelsAtPoint(lon, lat)
        .then((collection) => {
          highlightSource.clear();
          highlightSource.addFeatures(
            geojson.readFeatures(collection, { dataProjection: DATA_PROJECTION, featureProjection: MAP_PROJECTION }) as Feature[],
          );
          setSelection({ origin: 'click', parcels: collection.features.map((f) => f.properties) });
        })
        .catch((error: Error) => setNotice(error.message));
    });

    // Keep the canvas sized to its container (sidebar collapse, window resize).
    const resizeObserver = new ResizeObserver(() => map.updateSize());
    resizeObserver.observe(mapElement.current);

    return () => {
      resizeObserver.disconnect();
      map.setTarget(undefined);
      mapRef.current = null;
    };
  }, [highlightSource]);

  const zoomToProperty = useCallback(
    async (propertyId: string, pin: string) => {
      setNotice(null);
      try {
        const parcels = (await fetchPropertyParcels(propertyId)).filter((p) => p.status === 'Active' && p.geometryWkt);
        const wkt = new WKT();
        const features = parcels.map((p) => {
          const feature = wkt.readFeature(p.geometryWkt!, { dataProjection: DATA_PROJECTION, featureProjection: MAP_PROJECTION });
          feature.setId(p.id);
          return feature;
        });

        highlightSource.clear();
        setSelection({
          origin: 'property',
          pin,
          parcels: parcels.map((p) => ({
            parcelId: p.id,
            propertyId: p.propertyId,
            propertyIdentificationNumber: pin,
            lotNumber: p.lotNumber,
            blockNumber: p.blockNumber,
            surveyNumber: p.surveyNumber,
            barangayName: p.barangayName,
          })),
        });
        if (features.length === 0) {
          return;
        }

        highlightSource.addFeatures(features);
        const extent = createEmpty();
        features.forEach((f) => extend(extent, f.getGeometry()!.getExtent()));
        mapRef.current?.getView().fit(extent, { padding: [60, 60, 60, 60], maxZoom: 19, duration: 300 });
      } catch (error) {
        setNotice((error as Error).message);
      }
    },
    [highlightSource],
  );

  // Deep link from the Property Profile: /gis?propertyId=...
  const linkedPropertyId = searchParams.get('propertyId');
  useEffect(() => {
    if (!linkedPropertyId) {
      return;
    }
    fetchPropertyProfile(linkedPropertyId)
      .then((profile) => zoomToProperty(linkedPropertyId, profile.property.propertyIdentificationNumber))
      .catch((error: Error) => setNotice(error.message));
  }, [linkedPropertyId, zoomToProperty]);

  const search = useQuery({
    queryKey: ['gis', 'property-search', searchTerm],
    queryFn: () => apiGet<PagedResult<PropertyDto>>('/api/properties', { searchTerm, page: 1, pageSize: 10 }),
    enabled: searchTerm.length > 0,
  });

  const zoomedOut = zoom < mapConfig.parcelMinZoom;

  return (
    <div style={{ display: 'flex', flexWrap: 'wrap', gap: 16 }}>
      <div style={{ flex: '0 1 340px', minWidth: 0, maxHeight: 'calc(100vh - 112px)', overflowY: 'auto' }}>
        <Typography.Title level={3} style={{ marginTop: 0 }}>
          Tax Map
        </Typography.Title>

        <Input.Search
          placeholder="PIN, lot, title or survey no."
          allowClear
          enterButton
          aria-label="Search properties to locate on the map"
          onSearch={(value) => setSearchTerm(value.trim())}
        />

        {searchTerm && (
          <Spin spinning={search.isFetching}>
            <ul aria-label="Search results" style={{ listStyle: 'none', margin: '8px 0 0', padding: 0 }}>
              {search.data?.items.length === 0 && (
                <li>
                  <Typography.Text type="secondary">No matching properties</Typography.Text>
                </li>
              )}
              {search.data?.items.map((property) => (
                <li
                  key={property.id}
                  style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, padding: '8px 4px', borderBottom: '1px solid #f0f0f0' }}
                >
                  <div style={{ minWidth: 0 }}>
                    <Typography.Text strong ellipsis style={{ display: 'block' }}>
                      {property.propertyIdentificationNumber}
                    </Typography.Text>
                    <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                      {[property.barangayName, property.lotNumber && `Lot ${property.lotNumber}`].filter(Boolean).join(' · ')}
                    </Typography.Text>
                  </div>
                  <Button
                    size="small"
                    icon={<EnvironmentOutlined />}
                    aria-label={`Locate ${property.propertyIdentificationNumber} on the map`}
                    onClick={() => zoomToProperty(property.id, property.propertyIdentificationNumber)}
                  >
                    Locate
                  </Button>
                </li>
              ))}
            </ul>
          </Spin>
        )}

        {search.isError && (
          <Alert style={{ marginTop: 8 }} type="error" showIcon title={(search.error as Error).message} />
        )}

        {notice && <Alert style={{ marginTop: 12 }} type="info" showIcon closable onClose={() => setNotice(null)} title={notice} />}

        <SelectionPanel selection={selection} onOpenProfile={(propertyId) => navigate(`/properties/${propertyId}`)} />
      </div>

      <div style={{ flex: '1 1 480px', position: 'relative', height: 'calc(100vh - 112px)', minHeight: 400 }}>
        <div
          ref={mapElement}
          role="application"
          aria-label="Tax map. Use arrow keys to pan and plus/minus to zoom."
          tabIndex={0}
          style={{ position: 'absolute', inset: 0, border: '1px solid #d9d9d9', borderRadius: 8, overflow: 'hidden' }}
        />
        <div style={{ position: 'absolute', top: 8, left: '50%', transform: 'translateX(-50%)', display: 'flex', flexDirection: 'column', gap: 8, alignItems: 'center', pointerEvents: 'none' }}>
          {zoomedOut && <Tag color="blue">Zoom in to street level to show parcels</Tag>}
          {!zoomedOut && truncated && <Tag color="warning">Too many parcels in view — zoom in to see all of them</Tag>}
          {loadError && <Tag color="error">Could not load parcels: {loadError}</Tag>}
        </div>
      </div>
    </div>
  );
}

function SelectionPanel({ selection, onOpenProfile }: { selection: Selection | null; onOpenProfile: (propertyId: string) => void }) {
  if (!selection) {
    return (
      <Typography.Paragraph type="secondary" style={{ marginTop: 16 }}>
        Search for a property, or zoom in and click a parcel on the map.
      </Typography.Paragraph>
    );
  }

  if (selection.parcels.length === 0) {
    return (
      <Empty
        style={{ marginTop: 16 }}
        description={selection.origin === 'click' ? 'No mapped parcel at this point' : `${selection.pin} has no mapped parcel yet`}
      />
    );
  }

  return (
    <div style={{ marginTop: 16 }}>
      {selection.origin === 'click' && selection.parcels.length > 1 && (
        <Alert
          style={{ marginBottom: 12 }}
          type="warning"
          showIcon
          title={`${selection.parcels.length} parcels at this point`}
          description="Either the click is on a shared boundary, or the boundaries overlap — overlapping parcels are a data-quality issue to review."
        />
      )}
      {selection.parcels.map((parcel) => (
        <Card
          key={parcel.parcelId}
          size="small"
          title={parcel.propertyIdentificationNumber}
          style={{ marginBottom: 12 }}
          extra={
            <Button type="primary" size="small" onClick={() => onOpenProfile(parcel.propertyId)}>
              Open Property Profile
            </Button>
          }
        >
          <Descriptions column={1} size="small">
            <Descriptions.Item label="Barangay">{parcel.barangayName}</Descriptions.Item>
            <Descriptions.Item label="Lot">{parcel.lotNumber ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Block">{parcel.blockNumber ?? '—'}</Descriptions.Item>
            <Descriptions.Item label="Survey No.">{parcel.surveyNumber ?? '—'}</Descriptions.Item>
          </Descriptions>
        </Card>
      ))}
    </div>
  );
}
