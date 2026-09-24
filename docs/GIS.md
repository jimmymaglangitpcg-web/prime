# PRIME — GIS & Tax Mapping

Status: Phase 7 in progress (steps 1–3 and 4a done; 4b — map layers UI + printable tax map — remaining). Sections marked **(planned)** describe work not
yet implemented; everything else reflects code that exists and is tested.

## 1. Scope

CLAUDE.md §38/§54/§94: parcel geometry stored in PostGIS, a web map
(OpenLayers) showing parcels and reference layers, spatial search, and
parcel selection linking to the Property Profile.

## 2. Spatial reference system (decided 2026-09-24)

| Concern | Decision |
|---|---|
| Storage SRID | **EPSG:4326 (WGS84)** — `SpatialReference.StorageSrid` in `Prime.Domain/Common` is the single source for code; the column type repeats it for the database. |
| Column type | `Parcels.Geometry` is `geometry(MultiPolygon,4326)` — PostGIS rejects wrong-SRID and non-areal geometry itself. |
| Accepted input | WKT `POLYGON` or `MULTIPOLYGON`; a single polygon is normalized to a one-member MultiPolygon by `ParcelService.ParseParcelGeometry`. Anything else → `INVALID_GEOMETRY_TYPE`; unparseable/self-intersecting/empty → `INVALID_GEOMETRY`. |
| Area measurement | Never on raw degrees. `IGeometryMeasurementService` (Infrastructure, runs in PostGIS): geodesic `ST_Area(geom::geography)` by default, or `ST_Area(ST_Transform(geom, <srid>))` when `Gis:MeasurementSrid` is configured. |
| Measurement basis | Every measured area is returned with its basis (`GEODESIC_WGS84` or `PROJECTED_EPSG_<srid>`) so the figure is reproducible (CLAUDE.md §31/§77). |
| Declared vs measured | `Parcel.Area` is the declared area (title/technical description) and is never overwritten by the measured one. Both are exposed; reconciling a discrepancy is a human decision. |
| PRS92 input | Reprojected to 4326 on import (Phase 13 / GIS import), not stored in its source CRS. |

**Why WGS84 rather than a PRS92 zone for storage:** the target LGU is not
yet known, so a zone cannot be chosen without guessing (CLAUDE.md §5); the
web map, GeoJSON (RFC 7946) and most interchange formats expect WGS84; and
a single storage CRS keeps multi-zone or multi-LGU deployments possible.

**Why the measurement CRS is configurable:** Philippine cadastral survey
areas are computed on PRS92 grid coordinates. Geodesic area is accurate
but can differ slightly from a surveyor's grid area; configuring the LGU's
PRS92 zone makes measured areas comparable to survey figures.

> **DOMAIN VERIFICATION REQUIRED (per LGU):** which PRS92 zone
> (EPSG:3121–3125) applies to the LGU, confirmed against the LGU's actual
> survey plans / DENR-LMS data, before setting `Gis:MeasurementSrid`.

Datum note: PRS92 ↔ WGS84 conversion uses the transformation parameters in
PostGIS's `spatial_ref_sys` (PROJ). The accuracy of those parameters for a
given locality should be checked against known control points before
imported survey geometry is relied on for anything beyond display.

### Configuration

```json
"Gis": {
  "MeasurementSrid": null
}
```

`null` → geodesic. A value must be a positive EPSG code (validated at
startup); an EPSG code PostGIS doesn't know fails loudly at query time
rather than silently producing wrong areas.

## 3. Data model

- `Parcels.Geometry` — `geometry(MultiPolygon,4326)`, nullable (a parcel
  can be registered before it is mapped), GiST index `IX_Parcels_Geometry`.
- **Reference layers** — `BarangayBoundaries`, `ZoneBoundaries` (valuation
  zones), `RoadSegments` (migration `GisReferenceLayers`). Deliberately
  **not** a geometry column on `Barangay`/`Zone`: boundaries change
  (barangays are created/merged; valuation zones are redrawn with SMV
  revisions), and overwriting a shape would destroy history (CLAUDE.md
  §76). Each row is one **effective-dated version**:

  | Column | Meaning |
  |---|---|
  | `EffectiveDate` / `EndDate` | Half-open validity `[EffectiveDate, EndDate)`; `EndDate` null = current. Check constraint `EndDate > EffectiveDate`. |
  | `Source` (required) / `SourceReference` | Provenance of official map data, e.g. dataset + version, ordinance no. |
  | `ImportBatchId` | Every row written by one import; the audit log reason names the batch. |
  | key | `BarangayId` (matched by PSGC code), `ZoneId` (by zone code), or road `Code` (the source dataset's id). |

  A filtered unique index (`UX_*_Current`, `WHERE "EndDate" IS NULL`)
  makes "one current version per key" a database guarantee. Geometry:
  `MultiPolygon` for barangays/zones, `MultiLineString` for roads, SRID
  4326, GiST-indexed.

> **No real boundary data is loaded.** Barangay boundaries must come from
> an official source (e.g. PSA/NAMRIA, or the LGU's own cadastral/GIS
> office) and valuation-zone shapes from the LGU's approved SMV — PRIME
> does not invent them (CLAUDE.md §5/§81). Tests use DEMO shapes only.

### Import (`POST /api/gis/layers/{barangays|zones|roads}/import`)

Body: `{ effectiveDate, source, sourceReference?, featureCollection }`,
where `featureCollection` is RFC 7946 GeoJSON (WGS84). Feature properties:
barangays `psgcCode`; zones `zoneCode`; roads `code`, optional `name`,
`roadTypeCode`. The referenced barangay/zone/road type must already exist
in reference data.

- **`dryRun=true` is the default** — validates and reports counts
  (`newFeatures`, `supersededVersions`) without writing (CLAUDE.md §60
  VALIDATE → PREVIEW). `dryRun=false` commits **all or nothing**: any error
  → HTTP 422 with the full report and nothing written.
- Every problem is reported in one pass, per feature index: missing/
  duplicate key, unknown barangay/zone/road type, wrong geometry type,
  invalid (self-intersecting) geometry, coordinates outside WGS84 degree
  ranges (catches projected PRS92 metres exported without a `crs`), a
  non-WGS84 `crs` member, missing source/date, >10,000 features.
- Versioning: a feature whose key already has a current version closes it
  (`EndDate` = new `EffectiveDate`) and inserts the successor, in one
  transaction (close first, then insert — the unique index is not
  deferrable). A new version must be effective **after** every existing
  version of that key (`VERSION_NOT_AFTER_EXISTING`) — history is
  append-only; correcting a past version is not an import operation.
- Role gating (GIS_OFFICER only) is Phase 12; today any authenticated user
  can import. The full import workflow UI (upload, preview, error review)
  is Phase 13 — this is the API it will call.

### Query (`GET /api/gis/layers/{layer}?bbox=&asOf=&limit=`)

Versions valid on `asOf` (default: today, UTC date — same convention as
`ValuationService`) intersecting the bbox, as a GeoJSON FeatureCollection
with `key`, `name`, `effectiveDate`, `endDate`, `source`,
`sourceReference`. Same limit/`truncated` behaviour as parcels. Answers
"what did the tax map look like on date X" (CLAUDE.md §76). `EXPLAIN`
confirms the GiST index serves it (`IX_ZoneBoundaries_Geometry`).

## 4. API

All endpoints require authentication (role/permission gating is Phase 12).
Coordinates are WGS84 lon/lat throughout.

| Endpoint | Purpose |
|---|---|
| `GET /api/gis/parcels?bbox=minLon,minLat,maxLon,maxLat[&limit=]` | **Active** parcels intersecting the extent, as a GeoJSON FeatureCollection. Default 2000 features, max 5000 (a rendering limit, not a business rule); `truncated: true` + `limit` (RFC 7946 §6.1 foreign members) tell the client to zoom in. |
| `GET /api/gis/parcels/at?lon=&lat=` | Active parcels at a clicked point. Uses intersects, so a click on a shared edge returns both neighbours; more than one hit on an interior point means overlapping boundaries — a data-quality issue to surface, not hide. |
| `PUT /api/parcels/{id}/geometry` | Set or replace a boundary. Body `{ geometryWkt, version, reason }`. |

**Feature properties are identifiers only** — parcel/property ids, PIN,
lot/block/survey numbers, barangay. No owner names, TINs or values: the map
layer is a bulk listing, so personal data stays behind the Property Profile
(CLAUDE.md §68).

**Geometry replacement rules** (`ParcelService.SetGeometryAsync`):
- Only **Active** parcels (`PARCEL_NOT_ACTIVE` otherwise) — subdivided,
  consolidated or superseded parcels are history, not edited in place.
- `reason` is required when a boundary already exists; the audit log row
  records it together with the **previous WKT** in `OldValue`, so the
  earlier boundary is recoverable (CLAUDE.md §48/§76).
- Removing a boundary (empty WKT) is not supported.
- Optimistic concurrency: `version` is PostgreSQL's `xmin` (exposed as
  `ParcelDto.Version`, mapped via `IsRowVersion()` — no physical column). A
  stale version → HTTP 409 `PARCEL_CONCURRENCY_CONFLICT`, nothing written.
  This is the first concurrency token in PRIME; other mutable entities
  should follow the same pattern as update endpoints are added.

**Index use (verified).** All GIS reads go through one filter,
`GisService.ActiveParcelsIntersecting`, which EF translates to
`ST_Intersects`. `EXPLAIN` with sequential scans disabled (the dev table is
too small for the planner to prefer an index on its own) shows
`Index Scan using "IX_Parcels_Geometry"` with `Index Cond: ("Geometry" && …)`
— asserted by `GisQueryTests.SpatialFilter_TranslatesToIndexableSql_…`.
Re-check with real data volumes in Phase 14.

## 5. Frontend — Tax Map workspace (`/gis`)

`frontend/prime-web/src/pages/gis/GisWorkspacePage.tsx`, OpenLayers 10
(`ol`), sidebar entry "Tax Map".

- **Basemap** — XYZ tiles, configurable (`src/lib/mapConfig.ts`):

  | Env var | Default |
  |---|---|
  | `VITE_MAP_TILE_URL` | `https://tile.openstreetmap.org/{z}/{x}/{y}.png` |
  | `VITE_MAP_TILE_ATTRIBUTION` | OpenStreetMap contributors |
  | `VITE_MAP_INITIAL_CENTER` | `122.0,12.5` (Philippines) |
  | `VITE_MAP_INITIAL_ZOOM` | `6` |

  > **Deployment item:** the public OSM tile server is for development
  > only — its usage policy does not allow heavy production use. Each
  > deployment must point `VITE_MAP_TILE_URL` at an LGU-owned or
  > contracted tile service.
- **Parcel layer** — OpenLayers bbox loading strategy against
  `GET /api/gis/parcels`, only at zoom ≥ 14 (`mapConfig.parcelMinZoom`), so
  a province/country view requests nothing. A truncated response is not
  remembered as loaded, so zooming in re-fetches it; a "zoom in" tag is
  shown meanwhile.
- **Click** → `GET /api/gis/parcels/at` (server-side, so it is correct even
  if the visible layer was truncated) → highlighted boundary + selection
  card(s) with **Open Property Profile**. More than one hit shows a
  warning (shared boundary or overlap — data quality).
- **Search** → existing property search; **Locate** loads that property's
  Active parcels (`/api/properties/{id}/parcels`, WKT), fits the map and
  highlights them; a property with no mapped parcel says so.
- **Property Profile → "View on map"** opens `/gis?propertyId=…` zoomed to
  that property — both directions of §38's parcel ↔ profile link.
- Accessibility: the map container is focusable (arrow-key pan, +/- zoom),
  labelled as an application region; search results are a labelled list
  with per-row "Locate {PIN}" button names.
- Not in this step: drawing/editing boundaries in the browser (the PUT
  endpoint exists; an editing UI needs its own design pass), reference
  layers, printable tax map.

## 6. Verification so far

- `ParcelGeometryParsingTests` (unit): polygon normalization, SRID,
  rejection of points/lines/self-intersections/garbage.
- `ParcelGeometryStorageTests` (integration, real PostGIS 3.6.2):
  non-areal and wrong-SRID geometry rejected by the database; a bare
  polygon is stored as MultiPolygon (PostGIS 3.x auto-promotes); a
  ~0.001° square near 14.5°N measures ≈11,900 m² geodesically, and the
  PRS92 zone 3 projected area agrees within 0.5%.
- `GisBboxParsingTests` (unit): bbox order, bounds, culture invariance.
- `GisQueryTests` (integration): extent returns only Active intersecting
  parcels with correct GeoJSON; truncation flag; point lookup incl. shared
  edges; replacement requires a reason and audits the old WKT; stale
  version rejected with nothing written; historical parcels refused; HTTP
  400 codes for bad input; FeatureCollection JSON shape; GiST index use;
  OpenAPI document still generates.
- `ReferenceLayerTests` (integration): dry run writes nothing; commit →
  query returns provenance and the audit reason names the batch; a new
  version closes the old one and as-of queries return the right version
  on each side of the change date (half-open); a version not after the
  existing one is rejected with nothing changed; seven kinds of bad
  feature reported together with nothing partially imported; collection-
  level problems (non-WGS84 crs, empty, not a FeatureCollection); missing
  source/date; roads with road-type resolution; the database itself
  rejects two current versions and an inverted interval; HTTP 404 for an
  unknown layer, 422 for a failed commit, dry run by default.
- **Browser (Playwright, real API + real PostGIS, 2026-09-24):** 10-step
  flow — initial view makes no parcel requests; search → Locate fits and
  highlights; clicking parcel 1 / its east neighbour / empty ground selects
  the correct parcel or shows the empty state; Open Property Profile lands
  on the right property; View on map deep-links back; an unmapped property
  is reported; no horizontal overflow at 390 px; zero console errors.
  DEMO-labelled test rows were deleted afterwards.
