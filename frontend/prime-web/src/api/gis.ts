import { apiFetch, apiGet } from '../lib/apiClient';
import type { ParcelDto, ParcelFeatureCollection, PropertyProfileDto } from '../lib/types';

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
