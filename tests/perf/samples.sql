-- Inputs for tests/perf/measure.mjs, drawn from the DEMO volume set (generate-volume):
--   psql -h localhost -U prime -d prime_volume -At -f tests/perf/samples.sql > samples.json
SELECT json_build_object(
  'properties', (SELECT json_agg("Id") FROM (SELECT "Id" FROM "Property" WHERE "PropertyIdentificationNumber" LIKE 'DEMO-%' ORDER BY md5("Id"::text) LIMIT 200) p),
  'pins', (SELECT json_agg("PropertyIdentificationNumber") FROM (SELECT "PropertyIdentificationNumber" FROM "Property" WHERE "PropertyIdentificationNumber" LIKE 'DEMO-%' ORDER BY md5("Id"::text) LIMIT 200) p),
  'barangays', (SELECT json_agg(DISTINCT "BarangayId") FROM "Property" WHERE "PropertyIdentificationNumber" LIKE 'DEMO-%'),
  'municipalities', (SELECT json_agg(DISTINCT "MunicipalityId") FROM "Property" WHERE "PropertyIdentificationNumber" LIKE 'DEMO-%'),
  'smv', (SELECT "Id" FROM "Smvs" WHERE "OrdinanceNumber" = 'DEMOVOL-SMV-2027'),
  'sections', (SELECT json_agg("Id") FROM "TaxMapSections" WHERE "Remarks" = 'DEMO VOLUME section'),
  'surnames', (SELECT json_agg(DISTINCT substr("LastName", 6)) FROM "Taxpayers" WHERE "Address" LIKE 'DEMO address,%'),
  'points', (SELECT json_agg(json_build_array(ST_XMin("Geometry"), ST_YMin("Geometry"))) FROM (SELECT "Geometry" FROM "Parcels" pa JOIN "Property" p ON p."Id" = pa."PropertyId" WHERE p."PropertyIdentificationNumber" LIKE 'DEMO-%' ORDER BY md5(pa."Id"::text) LIMIT 200) g)
);
