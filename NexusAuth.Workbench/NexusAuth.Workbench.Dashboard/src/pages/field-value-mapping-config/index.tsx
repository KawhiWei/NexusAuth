import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, Form, Input, Pagination, Select, Space, Table, type TableProps } from 'tdesign-react';

type MappingStatus = '启用' | '停用';

type MappingItem = {
  id: string;
  fieldCode: string;
  fieldName: string;
  sourceValue: string;
  targetValue: string;
  status: MappingStatus;
  updatedAt: string;
};

type FilterState = {
  keyword: string;
  fieldCode: string;
  status: '' | MappingStatus;
};

const defaultFilters: FilterState = {
  keyword: '',
  fieldCode: '',
  status: '',
};

const sourceData: MappingItem[] = Array.from({ length: 58 }).map((_, index) => {
  const seq = index + 1;
  const fieldCode = `FIELD_${String((index % 8) + 1).padStart(2, '0')}`;
  return {
    id: `MAP-${String(seq).padStart(4, '0')}`,
    fieldCode,
    fieldName: `字段${(index % 8) + 1}取值映射`,
    sourceValue: `来源值_${seq}`,
    targetValue: `目标值_${(index % 12) + 1}`,
    status: index % 4 === 0 ? '停用' : '启用',
    updatedAt: `2026-03-${String((index % 28) + 1).padStart(2, '0')} 09:30`,
  };
});

const statusOptions = [
  { label: '全部状态', value: '' },
  { label: '启用', value: '启用' },
  { label: '停用', value: '停用' },
];

const FieldValueMappingConfigPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

  const fieldOptions = useMemo(() => {
    const unique = Array.from(new Set(sourceData.map((item) => item.fieldCode)));
    return [{ label: '全部字段', value: '' }, ...unique.map((value) => ({ label: value, value }))];
  }, []);

  const columns: TableProps<MappingItem>['columns'] = useMemo(
    () => [
      { colKey: 'id', title: '配置ID', width: 130 },
      { colKey: 'fieldCode', title: '字段编码', width: 130 },
      { colKey: 'fieldName', title: '字段名称', minWidth: 180, ellipsis: true },
      { colKey: 'sourceValue', title: '来源值', minWidth: 150, ellipsis: true },
      { colKey: 'targetValue', title: '目标值', minWidth: 150, ellipsis: true },
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
    ],
    [],
  );

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
        item.fieldName.toLowerCase().includes(keywordValue) ||
        item.sourceValue.toLowerCase().includes(keywordValue) ||
        item.targetValue.toLowerCase().includes(keywordValue);
      const matchesFieldCode = !appliedFilters.fieldCode || item.fieldCode === appliedFilters.fieldCode;
      const matchesStatus = !appliedFilters.status || item.status === appliedFilters.status;
      return matchesKeyword && matchesFieldCode && matchesStatus;
    });
  }, [appliedFilters]);

  const total = filteredData.length;

  useEffect(() => {
    const maxPage = Math.max(1, Math.ceil(total / pageSize));
    if (current > maxPage) {
      setCurrent(maxPage);
    }
  }, [current, pageSize, total]);

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
              clearable
              value={filters.keyword}
              placeholder="请输入配置ID/字段名称/来源值"
              style={{ width: 260 }}
              onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))}
            />
          </Form.FormItem>

          <Form.FormItem label="字段编码">
            <Select
              value={filters.fieldCode}
              style={{ width: 180 }}
              options={fieldOptions}
              onChange={(value) => setFilters((prev) => ({ ...prev, fieldCode: String(value ?? '') }))}
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
                  status: (value === '启用' || value === '停用' ? value : '') as '' | MappingStatus,
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

export default FieldValueMappingConfigPage;
