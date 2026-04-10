import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, Form, Input, Pagination, Select, Space, Table, type TableProps } from 'tdesign-react';

type MappingStatus = '启用' | '停用';
type SourceSystem = 'CRM' | 'OMS' | 'ERP';

type SaleRefIdMappingItem = {
  id: string;
  saleRefId: string;
  sourceSystem: SourceSystem;
  sourceValue: string;
  targetValue: string;
  status: MappingStatus;
  updatedAt: string;
};

type FilterState = {
  keyword: string;
  sourceSystem: '' | SourceSystem;
  status: '' | MappingStatus;
};

const defaultFilters: FilterState = {
  keyword: '',
  sourceSystem: '',
  status: '',
};

const sourceData: SaleRefIdMappingItem[] = Array.from({ length: 64 }).map((_, index) => {
  const seq = index + 1;
  const systems: SourceSystem[] = ['CRM', 'OMS', 'ERP'];
  return {
    id: `SRM-${String(seq).padStart(4, '0')}`,
    saleRefId: `SALE_REF_${String((index % 16) + 1).padStart(3, '0')}`,
    sourceSystem: systems[index % systems.length],
    sourceValue: `source_value_${seq}`,
    targetValue: `target_value_${(index % 20) + 1}`,
    status: index % 5 === 0 ? '停用' : '启用',
    updatedAt: `2026-03-${String((index % 28) + 1).padStart(2, '0')} 15:20`,
  };
});

const sourceSystemOptions = [
  { label: '全部系统', value: '' },
  { label: 'CRM', value: 'CRM' },
  { label: 'OMS', value: 'OMS' },
  { label: 'ERP', value: 'ERP' },
];

const statusOptions = [
  { label: '全部状态', value: '' },
  { label: '启用', value: '启用' },
  { label: '停用', value: '停用' },
];

const columns: TableProps<SaleRefIdMappingItem>['columns'] = [
  { colKey: 'id', title: '配置ID', width: 130 },
  { colKey: 'saleRefId', title: 'SaleRefId', width: 170 },
  { colKey: 'sourceSystem', title: '来源系统', width: 120 },
  { colKey: 'sourceValue', title: '来源值', minWidth: 180, ellipsis: true },
  { colKey: 'targetValue', title: '目标值', minWidth: 180, ellipsis: true },
  { colKey: 'status', title: '状态', width: 100 },
  { colKey: 'updatedAt', title: '更新时间', width: 180 },
  {
    colKey: 'operation',
    title: '操作',
    width: 160,
    fixed: 'right',
    cell: () => (
      <Space size="small">
        <Button variant="text" theme="primary" size="small">
          编辑
        </Button>
        <Button variant="text" theme="danger" size="small">
          删除
        </Button>
      </Space>
    ),
  },
];

const SaleRefIdMappingConfigPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const updateTableMaxHeight = () => {
      const baseHeight = Math.max(window.innerHeight - 200, 260);
      if (!tableWrapRef.current) {
        setTableMaxHeight(baseHeight);
        return;
      }
      const top = tableWrapRef.current.getBoundingClientRect().top;
      const next = Math.max(Math.floor(window.innerHeight - top - 110), 260);
      setTableMaxHeight(next);
    };

    updateTableMaxHeight();
    const frame = window.requestAnimationFrame(updateTableMaxHeight);
    window.addEventListener('resize', updateTableMaxHeight);

    return () => {
      window.cancelAnimationFrame(frame);
      window.removeEventListener('resize', updateTableMaxHeight);
    };
  }, []);

  const filteredData = useMemo(() => {
    const keywordValue = appliedFilters.keyword.trim().toLowerCase();
    return sourceData.filter((item) => {
      const matchesKeyword =
        keywordValue.length === 0 ||
        item.id.toLowerCase().includes(keywordValue) ||
        item.saleRefId.toLowerCase().includes(keywordValue) ||
        item.sourceValue.toLowerCase().includes(keywordValue) ||
        item.targetValue.toLowerCase().includes(keywordValue);
      const matchesSourceSystem = !appliedFilters.sourceSystem || item.sourceSystem === appliedFilters.sourceSystem;
      const matchesStatus = !appliedFilters.status || item.status === appliedFilters.status;
      return matchesKeyword && matchesSourceSystem && matchesStatus;
    });
  }, [appliedFilters]);

  const total = filteredData.length;

  const pagedData = useMemo(() => {
    const start = (current - 1) * pageSize;
    return filteredData.slice(start, start + pageSize);
  }, [current, filteredData, pageSize]);

  const handleQuery = () => {
    setAppliedFilters(filters);
    setCurrent(1);
  };

  const handleReset = () => {
    setFilters(defaultFilters);
    setAppliedFilters(defaultFilters);
    setCurrent(1);
  };

  return (
    <div>
      <Card bordered>
        <Form layout="inline">
          <Form.FormItem label="关键词">
            <Input
              value={filters.keyword}
              clearable
              placeholder="请输入配置ID/SaleRefId/来源值"
              style={{ width: 260 }}
              onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))}
            />
          </Form.FormItem>

          <Form.FormItem label="来源系统">
            <Select
              value={filters.sourceSystem}
              style={{ width: 160 }}
              options={sourceSystemOptions}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  sourceSystem: value === 'CRM' || value === 'OMS' || value === 'ERP' ? value : '',
                }))
              }
            />
          </Form.FormItem>

          <Form.FormItem label="状态">
            <Select
              value={filters.status}
              style={{ width: 140 }}
              options={statusOptions}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  status: value === '启用' || value === '停用' ? value : '',
                }))
              }
            />
          </Form.FormItem>

          <Form.FormItem>
            <Space>
              <Button theme="primary" onClick={handleQuery}>
                查询
              </Button>
              <Button variant="base" onClick={handleReset}>
                重置
              </Button>
            </Space>
          </Form.FormItem>
        </Form>
      </Card>

      <Card bordered style={{ marginTop: 16 }}>
        <div ref={tableWrapRef}>
          <Table
            rowKey="id"
            columns={columns}
            data={pagedData}
            verticalAlign="middle"
            maxHeight={tableMaxHeight}
            tableLayout="fixed"
          />
        </div>

        <div style={{ marginTop: 16, display: 'flex', justifyContent: 'flex-end' }}>
          <Pagination
            total={total}
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
        </div>
      </Card>
    </div>
  );
};

export default SaleRefIdMappingConfigPage;
