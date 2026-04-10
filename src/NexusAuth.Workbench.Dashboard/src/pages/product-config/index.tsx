import { useEffect, useMemo, useRef, useState } from 'react';
import { Button, Card, Checkbox, Form, Input, Pagination, Select, Space, Table, type TableProps } from 'tdesign-react';

type ProductStatus = '启用' | '停用';
type ProductType = '标准产品' | '增值产品' | '渠道产品';
type ReleaseChannel = '直销' | '代理' | '电商' | '生态';
type RiskLevel = '低' | '中' | '高';
type PriceMode = '固定价' | '阶梯价' | '协议价';

type ProductConfigItem = {
  id: string;
  productCode: string;
  productName: string;
  productType: ProductType;
  ownerDept: string;
  manager: string;
  releaseChannel: ReleaseChannel;
  riskLevel: RiskLevel;
  priceMode: PriceMode;
  monthlySales: number;
  effectiveDate: string;
  status: ProductStatus;
  version: string;
  updatedAt: string;
};

type GroupScenario = {
  label: string;
  value: string;
};

type ProductSceneGroup = {
  id: string;
  name: string;
  scenarios: GroupScenario[];
};

type FilterState = {
  keyword: string;
  productType: '' | ProductType;
  status: '' | ProductStatus;
};

const defaultFilters: FilterState = {
  keyword: '',
  productType: '',
  status: '',
};

const sourceData: ProductConfigItem[] = Array.from({ length: 240 }).map((_, index) => {
  const seq = index + 1;
  const productTypes: ProductType[] = ['标准产品', '增值产品', '渠道产品'];
  const releaseChannels: ReleaseChannel[] = ['直销', '代理', '电商', '生态'];
  const ownerDepts = ['产品中心', '营销中心', '运营中心', '渠道中心', '生态业务部'];
  const managers = ['张明', '李雪', '王鹏', '赵倩', '周航', '陈晨', '吴昊'];
  const riskLevel: RiskLevel = seq % 9 === 0 ? '高' : seq % 3 === 0 ? '中' : '低';
  const priceMode: PriceMode = seq % 10 === 0 ? '协议价' : seq % 4 === 0 ? '阶梯价' : '固定价';
  const releaseChannel = releaseChannels[(index * 2 + 1) % releaseChannels.length];
  const isDisabled = seq % 11 === 0 || seq % 17 === 0;
  const month = ((index % 12) + 1).toString().padStart(2, '0');
  const day = ((index % 27) + 1).toString().padStart(2, '0');
  const minute = ((index * 7) % 60).toString().padStart(2, '0');

  return {
    id: `PC-${String(seq).padStart(4, '0')}`,
    productCode: `PROD_${String((index % 48) + 1).padStart(3, '0')}`,
    productName: `${productTypes[index % productTypes.length]}产品 ${String((index % 48) + 1).padStart(2, '0')}`,
    productType: productTypes[index % productTypes.length],
    ownerDept: ownerDepts[(index + 2) % ownerDepts.length],
    manager: managers[(index * 3 + 1) % managers.length],
    releaseChannel,
    riskLevel,
    priceMode,
    monthlySales: 800 + ((seq * 137) % 9200),
    effectiveDate: `2026-${month}-${day}`,
    status: isDisabled ? '停用' : '启用',
    version: `v${Math.floor(index / 12) + 1}.${(index % 6) + 1}.0`,
    updatedAt: `2026-03-${String((index % 28) + 1).padStart(2, '0')} ${String(9 + (index % 10)).padStart(2, '0')}:${minute}`,
  };
});

const productTypeOptions = [
  { label: '全部类型', value: '' },
  { label: '标准产品', value: '标准产品' },
  { label: '增值产品', value: '增值产品' },
  { label: '渠道产品', value: '渠道产品' },
];

const statusOptions = [
  { label: '全部状态', value: '' },
  { label: '启用', value: '启用' },
  { label: '停用', value: '停用' },
];

const columns: TableProps<ProductConfigItem>['columns'] = [
  { colKey: 'id', title: '配置ID', width: 130 },
  { colKey: 'productCode', title: '产品编码', width: 140 },
  { colKey: 'productName', title: '产品名称', minWidth: 220, ellipsis: true },
  { colKey: 'productType', title: '产品类型', width: 120 },
  { colKey: 'ownerDept', title: '归属部门', width: 130 },
  { colKey: 'manager', title: '负责人', width: 100 },
  { colKey: 'releaseChannel', title: '发布渠道', width: 110 },
  { colKey: 'priceMode', title: '定价模式', width: 110 },
  { colKey: 'riskLevel', title: '风险等级', width: 100 },
  { colKey: 'monthlySales', title: '月销量', width: 110 },
  { colKey: 'effectiveDate', title: '生效日期', width: 130 },
  { colKey: 'version', title: '版本', width: 100 },
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
        <Button variant="text" theme="primary" size="small">
          详情
        </Button>
      </Space>
    ),
  },
];

const groupNameSeed = ['订单管理组', '库存管理组', '价格策略组', '渠道分发组', '结算核算组', '运营分析组'];
const sceneSeed: GroupScenario[] = [
  { label: '场景A', value: 'scene_a' },
  { label: '场景B', value: 'scene_b' },
  { label: '场景C', value: 'scene_c' },
  { label: '场景D', value: 'scene_d' },
];

const buildSceneGroups = (item: ProductConfigItem): ProductSceneGroup[] => {
  const suffix = Number.parseInt(item.id.replace('PC-', ''), 10) || 1;
  const groupCount = (suffix % 4) + 2;
  return Array.from({ length: groupCount }).map((_, index) => {
    const groupName = groupNameSeed[(index + suffix) % groupNameSeed.length];
    return {
      id: `group_${index + 1}`,
      name: `${groupName}${index + 1}`,
      scenarios: sceneSeed,
    };
  });
};

const ProductConfigPage = () => {
  const [filters, setFilters] = useState<FilterState>(defaultFilters);
  const [appliedFilters, setAppliedFilters] = useState<FilterState>(defaultFilters);
  const [current, setCurrent] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [expandedRowKeys, setExpandedRowKeys] = useState<Array<string | number>>([]);
  const [checkedSceneMap, setCheckedSceneMap] = useState<Record<string, Record<string, Array<string | number | boolean>>>>(
    {},
  );
  const [tableMaxHeight, setTableMaxHeight] = useState(() => Math.max(window.innerHeight - 200, 260));
  const tableWrapRef = useRef<HTMLDivElement | null>(null);

  const productSceneGroupMap = useMemo(() => {
    return sourceData.reduce<Record<string, ProductSceneGroup[]>>((prev, item) => {
      prev[item.id] = buildSceneGroups(item);
      return prev;
    }, {});
  }, []);

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
        item.productCode.toLowerCase().includes(keyword) ||
        item.productName.toLowerCase().includes(keyword);
      const matchesType = !appliedFilters.productType || item.productType === appliedFilters.productType;
      const matchesStatus = !appliedFilters.status || item.status === appliedFilters.status;
      return matchesKeyword && matchesType && matchesStatus;
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

  const handleSceneChange = (rowId: string, groupId: string, value: Array<string | number | boolean>) => {
    setCheckedSceneMap((prev) => ({
      ...prev,
      [rowId]: {
        ...(prev[rowId] || {}),
        [groupId]: value,
      },
    }));
  };

  return (
    <div>
      <Card bordered>
        <Form layout="inline">
          <Form.FormItem label="关键词">
            <Input
              value={filters.keyword}
              clearable
              placeholder="请输入配置ID/产品编码/产品名称"
              style={{ width: 260 }}
              onChange={(value) => setFilters((prev) => ({ ...prev, keyword: value }))}
            />
          </Form.FormItem>

          <Form.FormItem label="产品类型">
            <Select
              value={filters.productType}
              style={{ width: 160 }}
              options={productTypeOptions}
              onChange={(value) =>
                setFilters((prev) => ({
                  ...prev,
                  productType:
                    value === '标准产品' || value === '增值产品' || value === '渠道产品' ? value : '',
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
            expandedRowKeys={expandedRowKeys}
            onExpandChange={(keys) => setExpandedRowKeys(keys)}
            expandedRow={({ row }) => {
              const rowId = String(row.id);
              const sceneGroups = productSceneGroupMap[rowId] || [];
              return (
                <div style={{ padding: '8px 0' }}>
                  <Space direction="vertical" size={12} style={{ width: '100%' }}>
                    {sceneGroups.map((group) => (
                      <div
                        key={`${rowId}-${group.id}`}
                        style={{
                          background: 'var(--td-bg-color-container-hover)',
                          borderRadius: 6,
                          padding: '10px 12px',
                        }}
                      >
                        <div style={{ marginBottom: 8, fontWeight: 500 }}>{group.name}</div>
                        <Checkbox.Group
                          options={group.scenarios}
                          value={checkedSceneMap[rowId]?.[group.id] || []}
                          onChange={(value) => handleSceneChange(rowId, group.id, value)}
                        />
                      </div>
                    ))}
                  </Space>
                </div>
              );
            }}
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

export default ProductConfigPage;
