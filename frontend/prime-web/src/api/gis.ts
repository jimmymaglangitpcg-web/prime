import { apiFetch, apiGet } from '../lib/apiClient';
import type {
  ParcelDto,
  ParcelFeatureCollection,
  PropertyProfileDto,
  ReferenceLayerFeatureCollection,
  ReferenceLayerName,
  LandValueFeatureCollection,
  TaxMapSheetDto,
  TaxMapSheetRoute,
} from '../lib/types';

/** Formats a WGS84 coordinate for a query string (7 dp ≈ 1 cm). */
const coord = (n: number) => n.toFixed(7);

/** bbox = [minLon, minLat, maxLon, maxLat] in WGS84 degrees. */
export function fetchParcelsInExtent(bbox: [number, number, number, number]): Promise<ParcelFeatureCollection> {
  return apiFetch<ParcelFeatureCollection>(`/api/gis/parcels?bbox=${bbox.map(coord).join(',')}`);
}

export function fetchParcelsAtPoint(lon: number, lat: number): Promise<ParcelFeatureCollection> {
  return apiFetch<ParcelFeatureCollection>(`/api/gis/parcels/at?lon=${coord(lon)}&lat=${coord(lat)}`);
}

export function fetchPropertyParcels(propertyId: string): Promise<ParcelDto[]> {
  return apiGet<ParcelDto[]>(`/api/properties/${propertyId}/parcels`);
}

export function fetchPropertyProfile(propertyId: string): Promise<PropertyProfileDto> {
  return apiGet<PropertyProfileDto>(`/api/properties/${propertyId}`);
}

/** A tax map or index map sheet (id = section, barangay or municipality, by kind). */
export function fetchTaxMapSheet(kind: TaxMapSheetRoute, id: string, asOf: string, districtId?: string | null): Promise<TaxMapSheetDto> {
  return apiGet<TaxMapSheetDto>(`/api/gis/sheets/${kind}/${id}`, { asOf, districtId });
}

/** Reference-layer versions valid on `asOf` (YYYY-MM-DD) within a WGS84 bbox. */
export function fetchReferenceLayer(
  layer: ReferenceLayerName,
  bbox: [number, number, number, number],
  asOf: string,
): Promise<ReferenceLayerFeatureCollection> {
  return apiFetch<ReferenceLayerFeatureCollection>(
    `/api/gis/layers/${layer}?bbox=${bbox.map(coord).join(',')}&asOf=${encodeURIComponent(asOf)}`,
  );
}

/** The land value map in a WGS84 bbox: each land parcel's unit value under the SMV (none: the approved SMV in force on `asOf`). */
export function fetchValueMap(bbox: [number, number, number, number], smvId: string | null, asOf: string): Promise<LandValueFeatureCollection> {
  return apiFetch<LandValueFeatureCollection>(
    `/api/gis/value-map?bbox=${bbox.map(coord).join(',')}&asOf=${encodeURIComponent(asOf)}${smvId ? `&smvId=${smvId}` : ''}`,
  );
}
