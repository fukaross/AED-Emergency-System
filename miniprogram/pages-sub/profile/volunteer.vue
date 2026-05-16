<template>
  <view class="va-page" :class="themeClass">
    <custom-navbar title="加入志愿者" :show-back="true" :back-icon="BACK_ICON" />

    <scroll-view class="va-scroll" scroll-y>
      <view class="va-card">
        <view
          v-for="field in SINGLE_IMAGE_FIELDS"
          :key="field.key"
          class="va-image-block"
        >
          <text class="va-label va-label--required">{{ field.label }}</text>
          <view class="va-upload-box" @tap="chooseSingleImage(field.key)">
            <image
              v-if="form.singleImages[field.key]"
              class="va-upload-img"
              :src="form.singleImages[field.key]?.localPath || form.singleImages[field.key]?.url"
              mode="aspectFill"
            />
            <text v-else class="va-upload-text">{{ field.hint }}</text>
          </view>
        </view>
      </view>

      <view class="va-card">
        <view
          v-for="field in TEXT_FIELDS.filter((item) => !item.multiline)"
          :key="field.key"
          class="va-row"
        >
          <text class="va-label" :class="{ 'va-label--required': field.required }">
            {{ field.label }}
          </text>
          <input
            class="va-input"
            :value="form.text[field.key]"
            :placeholder="field.placeholder"
            @input="setTextValue(field.key, $event)"
          />
        </view>

        <view class="va-row">
          <text class="va-label va-label--required">出生日期</text>
          <picker mode="date" :value="form.birthDate" @change="setBirthDate">
            <view class="va-picker">
              {{ form.birthDate || '选择日期' }}
            </view>
          </picker>
        </view>

        <view v-for="field in SELECT_FIELDS" :key="field.key" class="va-row">
          <text class="va-label va-label--required">{{ field.label }}</text>
          <picker
            :range="field.options"
            range-key="label"
            :value="getSelectIndex(field.key)"
            @change="setSelectValue(field.key, $event)"
          >
            <view class="va-picker">
              {{ form.selects[field.key] || field.placeholder }}
            </view>
          </picker>
        </view>
      </view>

      <view class="va-card">
        <view
          v-for="field in MULTI_IMAGE_FIELDS"
          :key="field.key"
          class="va-image-block"
        >
          <text class="va-label va-label--required">{{ field.label }}</text>
          <view class="va-image-grid">
            <view
              v-for="(image, index) in form.multiImages[field.key]"
              :key="`${field.key}-${index}`"
              class="va-thumb"
            >
              <image
                class="va-thumb-img"
                :src="image.localPath || image.url"
                mode="aspectFill"
              />
              <text
                class="va-thumb-remove"
                @tap.stop="removeMultiImage(field.key, index)"
              >
                ×
              </text>
            </view>
            <view class="va-upload-box va-upload-box--small" @tap="chooseMultiImage(field.key)">
              <text class="va-upload-text">{{ field.hint }}</text>
            </view>
          </view>
        </view>
      </view>

      <view class="va-card">
        <view
          v-for="field in TEXT_FIELDS.filter((item) => item.multiline)"
          :key="field.key"
          class="va-textarea-block"
        >
          <text class="va-label va-label--required">{{ field.label }}</text>
          <textarea
            class="va-textarea"
            :value="form.text[field.key]"
            :placeholder="field.placeholder"
            maxlength="500"
            @input="setTextValue(field.key, $event)"
          />
        </view>

        <view class="va-commitment">
          <text class="va-label va-label--required">入队承诺</text>
          <text class="va-commitment-text">{{ COMMITMENT_TEXT }}</text>
          <view class="va-check-row" @tap="toggleCommitment">
            <view class="va-checkbox" :class="{ 'va-checkbox--checked': form.commitmentAccepted }">
              <text v-if="form.commitmentAccepted" class="va-checkbox-mark">✓</text>
            </view>
            <text class="va-check-text">我同意</text>
          </view>
        </view>
      </view>

      <view class="va-submit-wrap">
        <view
          class="va-submit"
          :class="{ 'va-submit--disabled': submitting.value }"
          @tap="submitForm"
        >
          <text>{{ submitting.value ? '提交中' : '提交' }}</text>
        </view>
      </view>
    </scroll-view>
  </view>
</template>

<script setup lang="ts">
import CustomNavbar from '@/components/custom-navbar/index.vue'
import { useTheme } from '@/composables/useTheme'
import {
  COMMITMENT_TEXT,
  MULTI_IMAGE_FIELDS,
  SELECT_FIELDS,
  SINGLE_IMAGE_FIELDS,
  TEXT_FIELDS,
} from './volunteer-fields'
import { useVolunteerApplicationForm } from './useVolunteerApplicationForm'

const BACK_ICON = '/static/icons/ico-left-arrow.svg'

const { themeClass } = useTheme()
const {
  form,
  submitting,
  chooseMultiImage,
  chooseSingleImage,
  getSelectIndex,
  removeMultiImage,
  setBirthDate,
  setSelectValue,
  setTextValue,
  submitForm,
  toggleCommitment,
} = useVolunteerApplicationForm()
</script>

<style src="./volunteer.scss" lang="scss" scoped></style>
