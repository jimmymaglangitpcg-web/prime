import { LAYERS, type MapLayerName } from '../../lib/mapLayers';

/** Small key matching a layer's map style — used by the layer panel and the print legend. */
export function LegendSwatch({ name }: { name: MapLayerName }) {
  const def = LAYERS[name];
  const isLine = name === 'roads';
  return (
    <span
      aria-hidden
      style={{
        display: 'inline-block',
        width: 18,
        height: isLine ? 0 : 12,
        verticalAlign: 'middle',
        borderStyle: def.lineDash ? 'dashed' : 'solid',
        borderColor: def.stroke,
        borderWidth: isLine ? '3px 0 0 0' : 2,
        background: def.fill ?? 'transparent',
        // Keep the swatch colours when printing (browsers drop backgrounds by default).
        printColorAdjust: 'exact',
        WebkitPrintColorAdjust: 'exact',
      }}
    />
  );
}
