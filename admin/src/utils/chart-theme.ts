import { use } from 'echarts/core'
import { LineChart, BarChart, PieChart } from 'echarts/charts'
import {
  GridComponent,
  TooltipComponent,
  LegendComponent,
  TitleComponent,
} from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import type { EChartsCoreOption } from 'echarts/core'
import type {
  AdminDashboardTrendPoint,
  AdminDashboardProcessingPoint,
  AdminDashboardStatusSlice,
} from '@/types/dashboard'

let registered = false

/** 按需注册 ECharts 模块（折线/柱/饼 + 必要组件 + Canvas 渲染器），控制打包体积。 */
export function ensureEchartsRegistered(): void {
  if (registered) return
  use([
    LineChart,
    BarChart,
    PieChart,
    GridComponent,
    TooltipComponent,
    LegendComponent,
    TitleComponent,
    CanvasRenderer,
  ])
  registered = true
}

// 统一克制色板（保持蓝主色 + 绿/青/琥珀语义色）
const COLORS = {
  axisLine: '#e8eaed',
  axisLabel: '#9ca3af',
  splitLine: '#f1f3f5',
  text: '#303133',
}

// 求助状态 → 颜色（绿为主体给安全感，琥珀引注意，灰示弱）
const STATUS_COLORS: Record<string, string> = {
  closed: '#2bb47e',
  false_alarm: '#c7ccd4',
  pending_review: '#e6a23c',
  published: '#79bbff',
  responding: '#a7d3ff',
}

const STATUS_LABELS: Record<string, string> = {
  closed: '已关闭',
  false_alarm: '误报',
  pending_review: '待审核',
  published: '进行中',
  responding: '响应中',
}

const ANIMATION = { animation: true, animationDuration: 600, animationEasing: 'cubicOut' } as const

const TOOLTIP = {
  backgroundColor: 'rgba(255, 255, 255, 0.96)',
  borderColor: '#eef0f2',
  borderWidth: 1,
  borderRadius: 8,
  padding: [8, 12],
  textStyle: { color: COLORS.text, fontSize: 12 },
  extraCssText: 'box-shadow: 0 4px 16px rgba(15, 23, 42, 0.10);',
} as const

function shortDate(isoDate: string): string {
  return isoDate.length >= 10 ? isoDate.slice(5) : isoDate
}

/** 折线（带渐变面积填充），用户增长 / 急救员增长共用。 */
export function buildGrowthLineOption(
  points: AdminDashboardTrendPoint[],
  color: string,
  seriesName: string,
): EChartsCoreOption {
  return {
    ...ANIMATION,
    color: [color],
    grid: { top: 16, right: 16, bottom: 28, left: 40 },
    tooltip: { ...TOOLTIP, trigger: 'axis' },
    xAxis: {
      type: 'category',
      data: points.map((point) => shortDate(point.date)),
      boundaryGap: false,
      axisLine: { lineStyle: { color: COLORS.axisLine } },
      axisTick: { show: false },
      axisLabel: { color: COLORS.axisLabel, fontSize: 11, interval: 4 },
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: COLORS.axisLabel, fontSize: 11 },
      splitLine: { lineStyle: { color: COLORS.splitLine, type: 'dashed' } },
    },
    series: [
      {
        name: seriesName,
        type: 'line',
        smooth: true,
        symbol: 'none',
        lineStyle: { width: 2.5 },
        areaStyle: {
          color: {
            type: 'linear',
            x: 0, y: 0, x2: 0, y2: 1,
            colorStops: [
              { offset: 0, color: `${color}33` },
              { offset: 1, color: `${color}00` },
            ],
          },
        },
        data: points.map((point) => point.total),
      },
    ],
  }
}

/** 求助处理概览：堆叠柱（已处理=绿 / 处理中=琥珀），全绿示意「处理得过来」。 */
export function buildProcessingOption(points: AdminDashboardProcessingPoint[]): EChartsCoreOption {
  return {
    ...ANIMATION,
    color: ['#2bb47e', '#e6a23c'],
    grid: { top: 36, right: 16, bottom: 28, left: 32 },
    tooltip: { ...TOOLTIP, trigger: 'axis', axisPointer: { type: 'shadow' } },
    legend: {
      data: ['已处理', '处理中'],
      top: 4, right: 8,
      icon: 'roundRect', itemWidth: 10, itemHeight: 10,
      textStyle: { color: COLORS.axisLabel, fontSize: 12 },
    },
    xAxis: {
      type: 'category',
      data: points.map((point) => shortDate(point.date)),
      axisLine: { lineStyle: { color: COLORS.axisLine } },
      axisTick: { show: false },
      axisLabel: { color: COLORS.axisLabel, fontSize: 11 },
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: COLORS.axisLabel, fontSize: 11 },
      splitLine: { lineStyle: { color: COLORS.splitLine, type: 'dashed' } },
    },
    series: [
      { name: '已处理', type: 'bar', stack: 'total', barWidth: '52%', itemStyle: { borderRadius: [0, 0, 0, 0] }, data: points.map((p) => p.processed) },
      { name: '处理中', type: 'bar', stack: 'total', barWidth: '52%', itemStyle: { borderRadius: [3, 3, 0, 0] }, data: points.map((p) => p.pending) },
    ],
  }
}

/** 求助状态环形图：中心总数，绿为主体。 */
export function buildStatusOption(slices: AdminDashboardStatusSlice[]): EChartsCoreOption {
  const total = slices.reduce((sum, slice) => sum + slice.count, 0)
  return {
    ...ANIMATION,
    tooltip: { ...TOOLTIP, trigger: 'item', formatter: '{b}: {c} ({d}%)' },
    title: {
      text: String(total),
      subtext: '总求助',
      left: '38%', top: '40%',
      textAlign: 'center',
      textStyle: { fontSize: 28, fontWeight: 600, color: COLORS.text },
      subtextStyle: { fontSize: 12, color: COLORS.axisLabel },
    },
    legend: {
      orient: 'vertical', right: 8, top: 'center',
      icon: 'circle', itemWidth: 8, itemHeight: 8,
      textStyle: { color: COLORS.axisLabel, fontSize: 12 },
    },
    series: [
      {
        type: 'pie',
        radius: ['58%', '78%'],
        center: ['38%', '50%'],
        avoidLabelOverlap: false,
        label: { show: false },
        labelLine: { show: false },
        itemStyle: { borderColor: '#ffffff', borderWidth: 3 },
        data: slices.map((slice) => ({
          value: slice.count,
          name: STATUS_LABELS[slice.status] ?? slice.status,
          itemStyle: { color: STATUS_COLORS[slice.status] ?? '#c7ccd4' },
        })),
      },
    ],
  }
}
