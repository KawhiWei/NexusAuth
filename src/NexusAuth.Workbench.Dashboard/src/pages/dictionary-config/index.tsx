import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, Form, Input, Pagination, Select, Space, Table, type TableProps } from 'tdesign-react';

type DictionaryStatus = '启用' | '停用';
type DictionaryCategory = '基础字典' | '业务字典' | '系统字典';

type DictionaryItem = {
  id: string;
  dictCode: string;
  dictName: string;
  category: DictionaryCategory;
  itemCount: number;
  status: DictionaryStatus;
  updatedAt: string;
};

type FilterState = {
  keyword: string;
  category: '' | DictionaryCategory;
  status: '' | DictionaryStatus;
};

const defaultFilters: FilterState = {
  keyword: '',
  category: '',
  status: '',
};

const sourceData: DictionaryItem[] = Array.from({ length: 86 }).map((_, index) => {
  const seq = index + 1;
  const categories: DictionaryCategory[] = ['基础字典', '业务字典', '系统字典'];
  return {
    id: `DICT-${String(seq).padStart(4, '0')}`,
    dictCode: `DIC_${String((index % 24) + 1).padStart(3, '0')}`,
    dictName: `数据字典 ${(index % 24) + 1}`,
    category: categories[index % categories.length],
    itemCount: ((index % 12) + 1) * 5,
    status: index % 7 === 0 ? '停用' : '启用',
    updatedAt: `2026-03-${String((index % 28) + 1).padStart(2, '0')} 10:20`,
  };
});

const categoryOptions = [
  { label: '全部分类', value: '' },
  { label: '基础字典', value: '基础字典' },
  { label: '业务字典', value: '业务字典' },
  { label: '系统字典', value: '系统字典' },
];

const statusOptions = [
  { label: '全部状态', value: '' },
  { label: '启用', value: '启用' },
  { label: '停用', value: '停用' },
];

const columns: TableProps<DictionaryItem>['columns'] = [
  { colKey: 'id', title: '字典ID', width: 130 },
  { colKey: 'dictCode', title: '字典编码', width: 150 },
  { colKey: 'dictName', title: '字典名称', minWidth: 220, ellipsis: true },
  { colKey: 'category', title: '字典分类', width: 130 },
  { colKey: 'itemCount', title: '条目数', width: 120 },
  { colKey: 'status', title: '状态', width: 100 },
  { colKey: 'updatedAt', title: '更新时间', width: 180 },
  {
    colKey: 'operation',
    title: '操作',
    width: 170,
    fixed: 'right',
    cell: () => (
      <Space size="small">
        <Button variant="text" theme="primary" size="small">
          编辑
        </Button>
        <Button variant="text" theme="primary" size="small">
          详情
        </Button>
      </Space>
    ),
  },
];

const DictionaryConfigPage = () => {
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
    const keyword = appliedFilters.keyword.trim().toLowerCase();
    return sourceData.filter((item) => {
      const matchesKeyword =
        !keyword ||
        item.id.toLowerCase().includes(keyword) ||
        item.dictCode.toLowerCase().includes(keyword) ||
        item.dictName.toLowerCase().includes(keyword);
      const matchesCategory = !appliedFilters.category || item.category === appliedFilters.category;
      const matchesStatus = !appliedFilters.status || item.status === appliedFilters.status;
      return matchesKeyword && matchesCategory && matchesStatus;
    });
  }, [appliedFilters]);

  const total = filteredData.length;

  const pagedData = useMemo(() => {
    const start = (current - 1) * pageSize;
    return filteredData.slice(start, start + pageSize);
  }, [current, filteredData, pageSize]);

  const onCurrentChange = (nextCurrent: number) => {
    setCurrent(nextCurrent);
  };

  const onPageSizeChange = (nextPageSize: string | number) => {
    setPageSize(Number(nextPageSize));
    setCurrent(1);
  };

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
              placeholder="请输入字典ID/字典编码/字典名称"
              style={{ width: 280 }}
              onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))}
            />
          </Form.FormItem>

          <Form.FormItem label="字典分类">
            <Select
              value={filters.category}
              style={{ width: 160 }}
              options={categoryOptions}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  category: value === '基础字典' || value === '业务字典' || value === '系统字典' ? value : '',
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
            onCurrentChange={onCurrentChange}
            onPageSizeChange={onPageSizeChange}
          />
        </div>
      </Card>
    </div>
  );
};

export default DictionaryConfigPage;
