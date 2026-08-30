---
description: Scaffold/refactor a TDesign searchable table page using the field-value-mapping-config pattern
---

First, load and follow the skill `tdesign-table-search`.

Use the following as task input (if provided):
- Target page/path: $ARGUMENTS

Then create or refactor a page to match this structure:
- Search bar (`Form`, `Input`, optional `Select`)
- Query + reset actions
- Data table list
- Optional pagination

Required output:
- Full page component (not partial snippets)
- Typed row model + typed columns
- Controlled filters (`filters` + `appliedFilters`)
- Query/reset handlers
- Pagination (`current`, `pageSize`, `total`)
- Table-only scrolling when long list appears

Checklist:
1. Locate target page and route/menu integration when needed.
2. Build filter section using `Card + Form`.
3. Use two-stage filter state:
   - `filters` for UI editing
   - `appliedFilters` for query execution
4. Wire query/reset behavior:
   - Query: apply filters + reset page to 1
   - Reset: restore defaults + reset page to 1
5. Add pagination with page size switching support:
   - `showPageSize`
   - `onCurrentChange`
   - `onPageSizeChange` (reset page to 1)
6. Ensure table-only scrollbar for long rows:
   - use `maxHeight` on `Table`
   - fallback formula: `Math.max(window.innerHeight - 200, 260)`
   - required final approach: use container top offset (`tableWrapRef.current.getBoundingClientRect().top`) to compute remaining viewport height
   - run once on mount, once in `requestAnimationFrame`, and on `resize`
7. Keep UI compact and consistent with TDesign starter/base style.
8. Validate with:
   - `npx eslint <target-file>`
   - `npm run build`

Reference implementation pattern:

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

<Table rowKey="id" columns={columns} data={pagedData} maxHeight={tableMaxHeight} tableLayout="fixed" />

<Pagination
  total={filteredData.length}
  current={current}
  pageSize={pageSize}
  pageSizeOptions={[10, 20, 50]}
  showPageSize
  showJumper
  onCurrentChange={(next) => setCurrent(next)}
  onPageSizeChange={(size) => {
    setPageSize(Number(size));
    setCurrent(1);
  }}
/>
```
