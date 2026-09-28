# DEMO content pack

A sample LGU content pack for developing and testing the loader
(docs/analysis/lgu-content-pack.md). Every place, code and name is invented and
prefixed `DEMO`. Real packs (LAM, ordinance and LGU data) go in the gitignored
`lgu-content/` folder and are never committed (CLAUDE.md §118).

The map layer `gis/barangays.geojson` draws the two DEMO barangays of Town A
as small squares in the open Philippine Sea, so they cannot be mistaken for a
real boundary.

To preview this pack locally, point the API at this folder's parent:

    ContentPacks__RootPath=<repo>/samples

then `POST /api/content-packs/content-demo/preview`.
