import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Alert, Button, Space, Tag, Typography } from 'antd';
import { ArrowLeftOutlined, PrinterOutlined } from '@ant-design/icons';
import 'ol/ol.css';
import OlMap from 'ol/Map';
import View from 'ol/View';
import ScaleLine from 'ol/control/ScaleLine';
import Attribution from 'ol/control/Attribution';
import { fromLonLat } from 'ol/proj';
import {
  LAYER_ORDER,
  LAYERS,
  MAP_PROJECTION,
  createBaseLayer,
  createDataLayers,
  type MapLayerName,
} from '../../lib/mapLayers';
import { LegendSwatch } from './LegendSwatch';

// Sheet sized for A4 landscape minus the 10 mm @page margins (index.css).
const SHEET_WIDTH_MM = 277;
const MAP_HEIGHT_MM = 130;

const lguName = import.meta.env.VITE_LGU_NAME?.trim();
const lguOffice = import.meta.env.VITE_LGU_OFFICE?.trim();

interface PrintParams {
  center: [number, number];
  zoom: number;
  layers: MapLayerName[];
  asOf: string;
}

function parseParams(params: URLSearchParams): PrintParams | null {
  const center = params.get('center')?.split(',').map(Number);
  const zoom = Number(params.get('zoom'));
  const layers = (params.get('layers') ?? '').split(',').filter((l): l is MapLayerName => l in LAYERS);
  const asOf = params.get('asOf') ?? '';
  if (
    !center ||
    center.length !== 2 ||
    center.some((n) => !Number.isFinite(n)) ||
    !Number.isFinite(zoom) ||
    !/^\d{4}-\d{2}-\d{2}$/.test(asOf)
  ) {
    return null;
  }
  return { center: [center[0], center[1]], zoom, layers, asOf };
}

/**
 * Printable tax map (CLAUDE.md §38 "Print Tax Map", §57 GIS reports). A
 * fixed A4-landscape sheet at the workspace's centre and zoom (so the printed
 * scale matches the screen), with the layers and as-of date chosen there.
 * LGU name/office come from optional VITE_LGU_* settings until LGU
 * branding administration exists (§78/§85) — nothing is shown when unset,
 * rather than an invented name.
 */
export function GisPrintPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const params = useMemo(() => parseParams(searchParams), [searchParams]);
  const mapElement = useRef<HTMLDivElement>(null);

  const [ready, setReady] = useState(false);
  const [zoom, setZoom] = useState<number | null>(null);
  const [sources, setSources] = useState<string[]>([]);
  const [errors, setErrors] = useState<string[]>([]);
  const printedAt = useMemo(() => new Date().toLocaleString('en-PH', { dateStyle: 'long', timeStyle: 'short' }), []);

  useEffect(() => {
    if (!params || !mapElement.current) {
      return;
    }

    const dataLayers = createDataLayers(
      () => params.asOf,
      (name, status) => {
        if (status.error) {
          setErrors((e) => [...e, `${LAYERS[name].title}: ${status.error}`]);
        } else if (status.truncated) {
          setErrors((e) => [...e, `${LAYERS[name].title}: too many features for one sheet — some are missing; print a smaller area.`]);
        }
      },
    );
    for (const name of LAYER_ORDER) {
      dataLayers[name].setVisible(params.layers.includes(name));
    }

    const map = new OlMap({
      target: mapElement.current,
      pixelRatio: Math.max(2, window.devicePixelRatio), // crisp on paper
      controls: [new ScaleLine({ bar: true, text: true, minWidth: 140 }), new Attribution({ collapsible: false })],
      interactions: [],
      layers: [createBaseLayer(), ...LAYER_ORDER.map((name) => dataLayers[name])],
      // Same centre and zoom as the workspace: the printed scale matches the screen.
      view: new View({ projection: MAP_PROJECTION, center: fromLonLat(params.center), zoom: params.zoom, enableRotation: false, maxZoom: 21 }),
    });
    setZoom(params.zoom);

    // Provenance: the sources of the reference shapes actually on this sheet.
    map.on('rendercomplete', () => {
      const found = new Set<string>();
      for (const name of ['zones', 'barangays', 'roads'] as const) {
        if (!dataLayers[name].getVisible()) {
          continue;
        }
        for (const feature of dataLayers[name].getSource()?.getFeatures() ?? []) {
          const reference = feature.get('sourceReference') as string | null;
          found.add(reference ? `${feature.get('source')} (${reference})` : (feature.get('source') as string));
        }
      }
      setSources([...found].sort());
      setReady(true);
    });

    return () => map.setTarget(undefined);
  }, [params]);

  if (!params) {
    return (
      <Alert
        type="error"
        showIcon
        title="Invalid print request"
        description="Open the printable tax map from the Tax Map's Print button."
        action={<Button onClick={() => navigate('/gis')}>Back to Tax Map</Button>}
      />
    );
  }

  const hiddenAtScale = params.layers.filter((name) => zoom !== null && zoom < LAYERS[name].minZoom);

  return (
    <div>
      <Space className="no-print" style={{ marginBottom: 16 }} wrap>
        <Button icon={<ArrowLeftOutlined />} onClick={() => navigate(-1)}>
          Back
        </Button>
        <Button type="primary" icon={<PrinterOutlined />} disabled={!ready} onClick={() => window.print()}>
          Print
        </Button>
        {ready ? <Tag color="success">Ready to print</Tag> : <Tag color="processing">Loading map…</Tag>}
      </Space>

      {errors.length > 0 && (
        <Alert className="no-print" style={{ marginBottom: 16 }} type="warning" showIcon title="Some map data is incomplete" description={[...new Set(errors)].join(' ')} />
      )}

      <div style={{ overflowX: 'auto' }}>
        <section
          aria-label="Printable tax map"
          style={{ width: `${SHEET_WIDTH_MM}mm`, background: '#fff', color: '#000', padding: '4mm', boxSizing: 'border-box' }}
        >
          <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', borderBottom: '2px solid #000', paddingBottom: '2mm', marginBottom: '3mm' }}>
            <div>
              {lguName && <div style={{ fontSize: 13, fontWeight: 600 }}>{lguName}</div>}
              {lguOffice && <div style={{ fontSize: 12 }}>{lguOffice}</div>}
              <div style={{ fontSize: 20, fontWeight: 700 }}>Tax Map</div>
            </div>
            <div style={{ fontSize: 11, textAlign: 'right' }}>
              <div>Boundaries as of {params.asOf}</div>
              <div>Printed {printedAt}</div>
              <div>PRIME — Property Registry, Information, Mapping &amp; Evaluation System</div>
            </div>
          </header>

          <div style={{ position: 'relative', height: `${MAP_HEIGHT_MM}mm`, border: '1px solid #000' }}>
            <div ref={mapElement} style={{ position: 'absolute', inset: 0 }} />
            <div
              aria-label="North"
              style={{ position: 'absolute', top: 8, right: 8, background: 'rgba(255,255,255,0.85)', padding: '2px 8px', textAlign: 'center', fontWeight: 700, lineHeight: 1.1, border: '1px solid #000' }}
            >
              ▲<br />N
            </div>
          </div>

          <footer style={{ display: 'flex', gap: '8mm', marginTop: '3mm', fontSize: 11 }}>
            <div style={{ minWidth: '60mm' }}>
              <div style={{ fontWeight: 600, marginBottom: 2 }}>Legend</div>
              {params.layers.map((name) => (
                <div key={name}>
                  <LegendSwatch name={name} /> {LAYERS[name].title}
                  {hiddenAtScale.includes(name) && <em> — not shown at this scale</em>}
                </div>
              ))}
            </div>
            <div style={{ flex: 1 }}>
              <div style={{ fontWeight: 600, marginBottom: 2 }}>Data sources</div>
              {params.layers.includes('parcels') && <div>Parcels: PRIME parcel registry (current records at time of printing)</div>}
              {sources.map((s) => (
                <div key={s}>{s}</div>
              ))}
              {sources.length === 0 && params.layers.some((l) => l !== 'parcels') && (
                <div>
                  <em>No reference-layer shapes in this area.</em>
                </div>
              )}
              <Typography.Text style={{ fontSize: 10, display: 'block', marginTop: 4 }}>
                Map for reference. Boundaries are as recorded in PRIME and do not replace approved survey plans or technical descriptions.
              </Typography.Text>
            </div>
          </footer>
        </section>
      </div>
    </div>
  );
}
