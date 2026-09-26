import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Alert, Button, Space, Spin, Tag, Typography } from 'antd';
import { ArrowLeftOutlined, PrinterOutlined } from '@ant-design/icons';
import 'ol/ol.css';
import OlMap from 'ol/Map';
import View from 'ol/View';
import VectorLayer from 'ol/layer/Vector';
import type BaseLayer from 'ol/layer/Base';
import VectorSource from 'ol/source/Vector';
import GeoJSON from 'ol/format/GeoJSON';
import ScaleLine from 'ol/control/ScaleLine';
import Attribution from 'ol/control/Attribution';
import { transformExtent } from 'ol/proj';
import { Fill, Stroke, Style, Text } from 'ol/style';
import type { FeatureLike } from 'ol/Feature';
import { fetchTaxMapSheet } from '../../api/gis';
import { ApiRequestError } from '../../lib/apiClient';
import { DATA_PROJECTION, LAYERS, MAP_PROJECTION, createBaseLayer, createDataLayers, todayIso } from '../../lib/mapLayers';
import type { TaxMapSheetDto, TaxMapSheetRoute } from '../../lib/types';
import { LegendSwatch } from './LegendSwatch';

// Same A4-landscape sheet as the printable tax map (GisPrintPage). The manual's
// standard tax map size is DOMAIN VERIFICATION REQUIRED.
const SHEET_WIDTH_MM = 277;
const MAP_HEIGHT_MM = 130;

const lguName = import.meta.env.VITE_LGU_NAME?.trim();
const lguOffice = import.meta.env.VITE_LGU_OFFICE?.trim();

const KINDS: TaxMapSheetRoute[] = ['tax-map', 'section-index', 'barangay-index'];

const UNIT_COLOR = '#be123c';

function sheetStyle(kind: TaxMapSheetRoute) {
  const area = new Style({ stroke: new Stroke({ color: '#000', width: 3 }) });
  return (feature: FeatureLike) => {
    if (feature.get('role') === 'area') {
      return area;
    }
    // Index maps: each section or barangay outlined and labelled with its index number.
    const name = feature.get('name') as string | null;
    return new Style({
      stroke: new Stroke({ color: UNIT_COLOR, width: 2, lineDash: [12, 4, 2, 4] }),
      fill: new Fill({ color: 'rgba(190, 18, 60, 0.04)' }),
      text: new Text({
        text: kind === 'barangay-index' && name ? `${feature.get('label')}\n${name}` : (feature.get('label') as string),
        font: '700 16px system-ui, sans-serif',
        fill: new Fill({ color: UNIT_COLOR }),
        stroke: new Stroke({ color: '#fff', width: 4 }),
        overflow: true,
      }),
    });
  };
}

/**
 * The tax map book's sheets (MRPAAO Ch. II §2 C.4 and C.8; docs/analysis/property-identification.md
 * §3.7): a section's tax map with its parcel numbers, a barangay's section index map, and a
 * municipality's or district's barangay index map. Each sheet is fitted to the boundaries
 * PRIME has recorded, with the index numbers in its heading; what cannot be drawn is listed.
 */
export function TaxMapSheetPage() {
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const kind = params.get('kind') as TaxMapSheetRoute | null;
  const id = params.get('id');
  const districtId = params.get('districtId');
  const asOf = params.get('asOf') && /^\d{4}-\d{2}-\d{2}$/.test(params.get('asOf')!) ? params.get('asOf')! : todayIso();
  const valid = !!kind && KINDS.includes(kind) && !!id;

  const sheet = useQuery({
    queryKey: ['tax-map-sheet', kind, id, districtId, asOf],
    queryFn: () => fetchTaxMapSheet(kind!, id!, asOf, districtId),
    enabled: valid,
  });

  if (!valid) {
    return <Alert type="error" showIcon title="Invalid sheet request" description="Open a tax map or index map from Admin → Property Identification." />;
  }
  if (sheet.isLoading) {
    return <Spin />;
  }
  if (sheet.isError || !sheet.data) {
    return (
      <Alert type="error" showIcon title="Sheet not available"
        description={sheet.error instanceof ApiRequestError ? sheet.error.apiError.message : (sheet.error as Error)?.message}
        action={<Button onClick={() => navigate(-1)}>Back</Button>} />
    );
  }
  return <Sheet kind={kind!} data={sheet.data} />;
}

function Sheet({ kind, data }: { kind: TaxMapSheetRoute; data: TaxMapSheetDto }) {
  const navigate = useNavigate();
  const mapElement = useRef<HTMLDivElement>(null);
  const [ready, setReady] = useState(false);
  const [zoom, setZoom] = useState<number | null>(null);
  const [errors, setErrors] = useState<string[]>([]);
  const printedAt = useMemo(() => new Date().toLocaleString('en-PH', { dateStyle: 'long', timeStyle: 'short' }), []);
  const withParcels = kind === 'tax-map';
  const sources = useMemo(
    () => [...new Set(data.features.map((f) => (f.properties.sourceReference ? `${f.properties.source} (${f.properties.sourceReference})` : f.properties.source)))].sort(),
    [data],
  );

  useEffect(() => {
    if (!mapElement.current || !data.extent) {
      return;
    }
    const outlines = new VectorLayer({
      source: new VectorSource({
        features: new GeoJSON().readFeatures(
          { type: 'FeatureCollection', features: data.features.map((f) => ({ ...f, properties: f.properties })) },
          { dataProjection: DATA_PROJECTION, featureProjection: MAP_PROJECTION },
        ),
      }),
      style: sheetStyle(kind),
      declutter: true,
      zIndex: 10,
    });
    const layers: BaseLayer[] = [createBaseLayer()];
    if (withParcels) {
      const dataLayers = createDataLayers(() => todayIso(), (name, status) => {
        if (status.error) {
          setErrors((e) => [...e, `${LAYERS[name].title}: ${status.error}`]);
        } else if (status.truncated) {
          setErrors((e) => [...e, `${LAYERS[name].title}: too many features for one sheet — some are missing.`]);
        }
      });
      layers.push(dataLayers.parcels);
    }
    const map = new OlMap({
      target: mapElement.current,
      pixelRatio: Math.max(2, window.devicePixelRatio),
      controls: [new ScaleLine({ bar: true, text: true, minWidth: 140 }), new Attribution({ collapsible: false })],
      interactions: [],
      layers: [...layers, outlines],
      view: new View({ projection: MAP_PROJECTION, enableRotation: false, maxZoom: 21 }),
    });
    map.getView().fit(transformExtent(data.extent, DATA_PROJECTION, MAP_PROJECTION), { padding: [24, 24, 24, 24] });
    setZoom(map.getView().getZoom() ?? null);
    map.on('rendercomplete', () => setReady(true));
    return () => map.setTarget(undefined);
  }, [data, kind, withParcels]);

  const parcelsHidden = withParcels && zoom !== null && zoom < LAYERS.parcels.minZoom;

  return (
    <div>
      <Space className="no-print" style={{ marginBottom: 16 }} wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(-1)}>Back</Button>
        <Button type="primary" icon={<PrinterOutlined />} disabled={!ready && !!data.extent} onClick={() => window.print()}>Print</Button>
        {data.extent ? (ready ? <Tag color="success">Ready to print</Tag> : <Tag color="processing">Loading map…</Tag>) : null}
      </Space>
      {errors.length > 0 && (
        <Alert className="no-print" style={{ marginBottom: 16 }} type="warning" showIcon title="Some map data is incomplete" description={[...new Set(errors)].join(' ')} />
      )}
      <div style={{ overflowX: 'auto' }}>
        <section aria-label={data.title} style={{ width: `${SHEET_WIDTH_MM}mm`, background: '#fff', color: '#000', padding: '4mm', boxSizing: 'border-box' }}>
          <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', borderBottom: '2px solid #000', paddingBottom: '2mm', marginBottom: '3mm' }}>
            <div>
              {lguName && <div style={{ fontSize: 13, fontWeight: 600 }}>{lguName}</div>}
              {lguOffice && <div style={{ fontSize: 12 }}>{lguOffice}</div>}
              <div style={{ fontSize: 20, fontWeight: 700 }}>{data.title}</div>
            </div>
            <table aria-label="Index numbers" style={{ fontSize: 11, borderCollapse: 'collapse' }}>
              <tbody>
                {data.heading.map((h) => (
                  <tr key={h.label}>
                    <td style={{ paddingRight: 8 }}>{h.label}:</td>
                    <td style={{ paddingRight: 8, fontWeight: 600 }}>{h.name ?? ''}</td>
                    <td>Index No. <b>{h.indexNumber ?? '—'}</b></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </header>

          {data.extent ? (
            <div style={{ position: 'relative', height: `${MAP_HEIGHT_MM}mm`, border: '1px solid #000' }}>
              <div ref={mapElement} style={{ position: 'absolute', inset: 0 }} />
              <div aria-label="North" style={{ position: 'absolute', top: 8, right: 8, background: 'rgba(255,255,255,0.85)', padding: '2px 8px', textAlign: 'center', fontWeight: 700, lineHeight: 1.1, border: '1px solid #000' }}>
                ▲<br />N
              </div>
            </div>
          ) : (
            <Alert type="warning" showIcon title="Nothing to draw" description="No boundary is recorded for this sheet on this date. Import the section or barangay boundaries under GIS first." />
          )}

          <footer style={{ display: 'flex', gap: '8mm', marginTop: '3mm', fontSize: 11 }}>
            <div style={{ minWidth: '60mm' }}>
              <div style={{ fontWeight: 600, marginBottom: 2 }}>Legend</div>
              {data.features.some((f) => f.properties.role === 'area') && (
                <div><span aria-hidden style={{ display: 'inline-block', width: 18, borderTop: '3px solid #000', verticalAlign: 'middle' }} /> {kind === 'tax-map' ? 'Section boundary' : 'Barangay boundary'}</div>
              )}
              {kind !== 'tax-map' && <div><LegendSwatch name={kind === 'section-index' ? 'sections' : 'barangays'} /> {kind === 'section-index' ? 'Section (index no.)' : 'Barangay (index no.)'}</div>}
              {withParcels && <div><LegendSwatch name="parcels" /> Parcel (parcel no.){parcelsHidden && <em> — not shown at this scale</em>}</div>}
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 600, marginBottom: 2 }}>Boundaries as of {data.asOf} · Printed {printedAt}</div>
              {sources.map((s) => <div key={s}>{s}</div>)}
              {withParcels && <div>Parcels: PRIME parcel registry (current records at time of printing)</div>}
              {data.missing.map((m) => <div key={m}><em>Not shown: {m}</em></div>)}
              <Typography.Text style={{ fontSize: 10, display: 'block', marginTop: 4 }}>
                Reference map (MRPAAO Ch. II layout; the standard sheet size and symbols are to be confirmed against the LAM). It does not replace approved survey plans.
              </Typography.Text>
            </div>
          </footer>
        </section>
      </div>
    </div>
  );
}
