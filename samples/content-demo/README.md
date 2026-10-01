# DEMO content pack

A sample LGU content pack for developing and testing the loader
(docs/analysis/lgu-content-pack.md). Every place, code and name is invented and
prefixed `DEMO`. Real packs (LAM, ordinance and LGU data) go in the gitignored
`lgu-content/` folder and are never committed (CLAUDE.md §118).

`catalogues/smv.json`, `valuation/smv-schedules.csv` and `valuation/assessment-levels.csv`
hold a DEMO certified SMV for Town A (effective 2099), two invented unit values and one
level. They import as drafts for a second user to approve. `valuation/adjustment-factors.json`
adds two invented factors; `building-costs.json`, `extra-item-costs.json` and
`depreciation-rates.json` add an invented construction cost, fence cost and depreciation table
for a DEMO structural type; `exchange-rates.csv` and `price-indices.csv` add two invented USD rates
(dated 2099) and one invented index for machinery.

The map layer `gis/barangays.geojson` draws the two DEMO barangays of Town A
as small squares in the open Philippine Sea, so they cannot be mistaken for a
real boundary.

To preview this pack locally, point the API at this folder's parent:

    ContentPacks__RootPath=<repo>/samples

then `POST /api/content-packs/content-demo/preview`.
