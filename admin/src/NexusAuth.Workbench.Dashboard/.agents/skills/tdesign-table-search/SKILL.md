---
name: tdesign-table-search
description: Build a TDesign starter/base style searchable table page with query/reset, pagination, and table-only scrolling.
compatibility: Generic project-level .agents skill format
---

Use this skill when user asks for a page that contains:
- Search/filter form
- Query + reset actions
- Table list
- Optional pagination

Deliverables (all required):
- A complete runnable page component (`index.tsx`)
- Typed mock row model and typed table columns
- Controlled filter state with query/reset handlers
- Pagination state (`current`, `pageSize`, `total`) and page-size switching handlers
- Table-only scrolling when rows exceed height (avoid page-level scrollbar)

Implementation blueprint (reference: `src/pages/field-value-mapping-config/index.tsx`):

1) Page structure
- Search card at top: `Card` + `Form` + `Input/Select`
- Actions: `Button theme="primary"` for query, `Button variant="base"` for reset
- Table card below: `Table` with stable `rowKey`
- Pagination aligned bottom-right

2) State contract
- Filters:
  - `filters`: form editing state
  - `appliedFilters`: state actually used by table query
- Pagination:
  - `current`, `pageSize`
  - derived `total`
- Scroll:
  - `tableMaxHeight` (dynamic)
  - optional `tableWrapRef` for more accurate viewport remaining-height calculation

3) Query behavior contract
- Query button:
  - set `appliedFilters = filters`
  - reset `current = 1`
- Reset button:
  - restore default filters
  - set `appliedFilters = defaultFilters`
  - set `current = 1`

4) Data derivation contract (`useMemo`)
- `filteredData`: derived from `sourceData` + `appliedFilters`
- `pagedData`: slice by `current` and `pageSize`
- `total = filteredData.length`

5) Pagination event contract
- Must include:
  - `onCurrentChange(next) => setCurrent(next)`
  - `onPageSizeChange(size) => setPageSize(Number(size)); setCurrent(1)`

6) Table scrolling contract (important)
- Baseline default (fallback only):
  - `Math.max(window.innerHeight - 200, 260)`
- Required robust version (same style as reference page):
  - compute by `tableWrapRef.current.getBoundingClientRect().top`
  - `maxHeight = Math.max(window.innerHeight - top - 110, 260)`
  - fallback to baseline when ref unavailable
  - run on mount, run again in `requestAnimationFrame`, and update on `resize`
- Goal: row overflow must scroll in table area, not whole page

Reference snippet:

```tsx
const [filters, setFilters] = useState(defaultFilters);
const [appliedFilters, setAppliedFilters] = useState(defaultFilters);
const [current, setCurrent] = useState(1);
const [pageSize, setPageSize] = useState(10);
const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));

const handleQuery = () => {
  setAppliedFilters(filters);
  setCurrent(1);
};

const handleReset = () => {
  setFilters(defaultFilters);
  setAppliedFilters(defaultFilters);
  setCurrent(1);
};

const filteredData = useMemo(() => {
  // filter by appliedFilters
}, [appliedFilters]);

const pagedData = useMemo(() => {
  const start = (current - 1) * pageSize;
  return filteredData.slice(start, start + pageSize);
}, [filteredData, current, pageSize]);
```

7) Visual and coding conventions
- Keep spacing compact and consistent with TDesign starter/base pages
- Prefer inline style for small spacing adjustments; avoid heavy custom CSS
- Use stable literal options and deterministic mock data
- Avoid `any` in newly generated page code

8) Verification checklist
- Ensure target page compiles and is directly runnable
- Prefer running `npx eslint <target-file>` when possible
- Must run `npm run build` and pass before finish
