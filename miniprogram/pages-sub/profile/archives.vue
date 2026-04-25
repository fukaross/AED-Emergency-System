<template>
  <view class="ar-root" :class="themeClass">
    <custom-navbar title="急救档案" :show-back="true" :back-icon="BACK_ICON" />

    <view class="ar-search">
      <image class="ar-search-icon" :src="SEARCH_ICON" mode="aspectFit" />
      <input
        v-model="keyword"
        class="ar-search-input"
        placeholder="搜索姓名..."
        placeholder-class="ar-search-placeholder"
        confirm-type="search"
        @confirm="handleSearchConfirm"
      />
    </view>

    <scroll-view class="ar-filter" scroll-x enhanced show-scrollbar="false">
      <view
        v-for="tag in FILTER_TAGS"
        :key="tag.value ?? 'all'"
        class="ar-filter-tag"
        :class="{ 'ar-filter-tag--active': activeLevel === tag.value }"
        @tap="handleLevelClick(tag.value)"
      >
        <text>{{ tag.label }}</text>
      </view>
    </scroll-view>

    <view class="ar-body">
      <view v-if="loading && items.length === 0" class="ar-state">
        <text class="ar-state-text">加载中...</text>
      </view>

      <view v-else-if="error" class="ar-state">
        <text class="ar-state-text">{{ error }}</text>
        <view class="ar-state-btn" @tap="handleRetry">重试</view>
      </view>

      <template v-else>
        <archive-card
          v-if="selfArchive"
          :item="selfArchive"
          :is-self="true"
          :can-view-full-phone="canViewFullPhone"
          @select="handleCardClick"
        />

        <view v-if="selfArchive && items.length > 0" class="ar-divider">
          <view class="ar-divider-line" />
          <text class="ar-divider-text">其他急救员</text>
          <view class="ar-divider-line" />
        </view>

        <view v-if="items.length > 0" class="ar-list">
          <archive-card
            v-for="item in items"
            :key="item.id"
            :item="item"
            :can-view-full-phone="canViewFullPhone"
            @select="handleCardClick"
          />
        </view>

        <view v-if="loadingMore" class="ar-state">
          <text class="ar-state-text">加载更多...</text>
        </view>

        <view v-if="!selfArchive && items.length === 0" class="ar-state">
          <text class="ar-state-text">暂无急救员档案</text>
        </view>
      </template>
    </view>

    <archive-phone-modal
      :visible="showPhoneModal"
      :archive-name="selectedArchive?.name ?? ''"
      :purpose="purpose"
      :full-phone="fullPhone"
      :phone-loading="phoneLoading"
      @update:purpose="purpose = $event"
      @close="closePhoneModal"
      @confirm="confirmViewPhone"
      @call="handleCall"
    />
  </view>
</template>

<script setup lang="ts">
import CustomNavbar from '@/components/custom-navbar/index.vue'
import ArchiveCard from './archive-card.vue'
import ArchivePhoneModal from './archive-phone-modal.vue'
import { useArchivesPage } from './useArchivesPage'
import { useTheme } from '@/composables/useTheme'

const BACK_ICON = '/static/icons/ico-left-arrow.svg'
const SEARCH_ICON = '/static/icons/search.svg'

const {
  FILTER_TAGS,
  activeLevel,
  canViewFullPhone,
  closePhoneModal,
  confirmViewPhone,
  error,
  fullPhone,
  handleCall,
  handleCardClick,
  handleLevelClick,
  handleRetry,
  handleSearchConfirm,
  items,
  keyword,
  loading,
  loadingMore,
  phoneLoading,
  purpose,
  selectedArchive,
  selfArchive,
  showPhoneModal,
} = useArchivesPage()
const { themeClass } = useTheme()
</script>

<style src="./archives.scss" lang="scss" scoped></style>
