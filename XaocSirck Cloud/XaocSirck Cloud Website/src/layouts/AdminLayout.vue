<script setup lang="ts">
import { useRoute } from 'vue-router'
import AppSidebar from '@/components/AppSidebar.vue'
import AppHeader from '@/components/AppHeader.vue'
import AppFooter from '@/components/AppFooter.vue'
import ToastContainer from '@/components/ToastContainer.vue'

const route = useRoute()
</script>

<template>
  <div class="admin-layout">
    <AppSidebar />
    <div class="main-area">
      <AppHeader :title="(route.meta.title as string) || '管理中心'" :subtitle="(route.meta.subtitle as string) || ''" />
      <main class="content">
        <RouterView />
      </main>
      <AppFooter />
    </div>
    <ToastContainer />
  </div>
</template>

<style scoped>
.admin-layout {
  display: flex;
  width: 100%;
  min-height: 100vh;
  background:
    radial-gradient(ellipse 60rem 40rem at 70% -10%, rgba(0, 122, 255, 0.06), transparent),
    radial-gradient(ellipse 50rem 30rem at 20% 110%, rgba(94, 92, 230, 0.04), transparent),
    #f2f2f7;
}

.main-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
  min-height: 100vh;
}

.content {
  flex: 1;
  padding: 1.5rem;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  min-width: 0;
}
</style>
