import { createRouter, createWebHistory } from 'vue-router'
import AdminLayout from '@/layouts/AdminLayout.vue'

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: '/',
      component: AdminLayout,
      children: [
        {
          path: '',
          name: 'dashboard',
          component: () => import('@/views/DashboardHome.vue'),
          meta: { title: '管理中心', subtitle: '系统总览' },
        },
        {
          path: 'system/errors',
          name: 'error-center',
          component: () => import('@/views/ErrorCenter.vue'),
          meta: { title: '错误中心', subtitle: '告警日志与回调追踪' },
        },
        {
          path: 'system/health',
          name: 'system-health',
          component: () => import('@/views/SystemHealth.vue'),
          meta: { title: '系统健康监控', subtitle: '资源占用与流量趋势' },
        },
        {
          path: 'system/security',
          name: 'security-ip',
          component: () => import('@/views/SecurityIP.vue'),
          meta: { title: '安全 / IP 管理', subtitle: 'IP 统计与黑名单' },
        },
        {
          path: 'cache/query',
          name: 'cache-query',
          component: () => import('@/views/CacheQuery.vue'),
          meta: { title: '缓存查询', subtitle: 'SHA256 查询' },
        },
        {
          path: 'cache/hot',
          name: 'hot-cache',
          component: () => import('@/views/HotCacheUpdate.vue'),
          meta: { title: '热缓存更新', subtitle: '上传 MPHF 文件' },
        },
        {
          path: 'cache/cold',
          name: 'cold-cache',
          component: () => import('@/views/ColdCacheManage.vue'),
          meta: { title: '冷缓存管理', subtitle: '条目增删与分页' },
        },
        {
          path: 'cache/service',
          name: 'service-control',
          component: () => import('@/views/ServiceControl.vue'),
          meta: { title: '服务控制', subtitle: '暂停 / 重启' },
        },
        {
          path: 'cache/flow',
          name: 'cache-flow',
          component: () => import('@/views/CacheUpdateFlow.vue'),
          meta: { title: '缓存更新流程', subtitle: '维护向导' },
        },
        {
          path: 'docs/api',
          name: 'api-docs',
          component: () => import('@/views/ApiDocs.vue'),
          meta: { title: 'API 文档', subtitle: '缓存服务接口说明' },
        },
        {
          path: 'settings/token',
          name: 'token-settings',
          component: () => import('@/views/TokenSettings.vue'),
          meta: { title: 'Token 配置', subtitle: 'JWT 鉴权凭证管理' },
        },
      ],
    },
  ],
})

export default router
