<template>
  <div
    v-if="editor"
    class="tiptap-wrapper"
    @dragover.prevent
    @drop.prevent="handleEditorDrop"
  >
    <div class="tiptap-toolbar">
      <button type="button" class="tbar-btn" :class="{ active: editor.isActive('bold') }" @click="editor.chain().focus().toggleBold().run()">B</button>
      <button type="button" class="tbar-btn" :class="{ active: editor.isActive('italic') }" @click="editor.chain().focus().toggleItalic().run()">I</button>
      <button type="button" class="tbar-btn" :class="{ active: editor.isActive('underline') }" @click="editor.chain().focus().toggleUnderline().run()">U</button>
      <button type="button" class="tbar-btn" :class="{ active: editor.isActive('heading', { level: 2 }) }" @click="editor.chain().focus().toggleHeading({ level: 2 }).run()">H2</button>
      <button type="button" class="tbar-btn" :disabled="uploading" @click="fileInput?.click()">🖼</button>
    </div>
    <editor-content :editor="editor" class="tiptap-content" />
    <input ref="fileInput" type="file" accept="image/*" style="display: none" @change="handleImageUpload">
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onBeforeUnmount } from 'vue'
import { ElMessage } from 'element-plus'
import { useEditor, EditorContent } from '@tiptap/vue-3'
import StarterKit from '@tiptap/starter-kit'
import Image from '@tiptap/extension-image'
import Link from '@tiptap/extension-link'
import Underline from '@tiptap/extension-underline'
import { uploadAdminImage, validateAdminImageFile } from '@/utils/admin-image-upload'

const props = defineProps<{ modelValue: string }>()
const emit = defineEmits<{ 'update:modelValue': [value: string] }>()

const fileInput = ref<HTMLInputElement | null>(null)
const uploading = ref(false)

const editor = useEditor({
  content: props.modelValue,
  extensions: [StarterKit, Image, Link.configure({ openOnClick: false }), Underline],
  onUpdate: ({ editor: ed }) => { emit('update:modelValue', ed.getHTML()) },
})

watch(() => props.modelValue, (val) => {
  if (editor.value && val !== editor.value.getHTML()) {
    editor.value.commands.setContent(val)
  }
})

async function handleImageUpload(e: Event) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  if (file) await uploadAndInsertImage(file)
  input.value = ''
}

async function handleEditorDrop(event: DragEvent) {
  const file = event.dataTransfer?.files?.[0]
  if (!file) return
  await uploadAndInsertImage(file)
}

async function uploadAndInsertImage(file: File) {
  if (!editor.value) return
  const validationError = validateAdminImageFile(file)
  if (validationError) {
    ElMessage.error(validationError)
    return
  }
  uploading.value = true
  try {
    const result = await uploadAdminImage(file, 'course')
    editor.value.chain().focus().setImage({ src: result.url }).run()
  } catch (error) {
    const message = error instanceof Error ? error.message : '图片上传失败'
    ElMessage.error(message)
  } finally {
    uploading.value = false
  }
}

onBeforeUnmount(() => { editor.value?.destroy() })
</script>

<style scoped>
.tiptap-wrapper { border: 1px solid #dcdfe6; border-radius: 4px; }
.tiptap-toolbar { display: flex; gap: 4px; padding: 6px 8px; border-bottom: 1px solid #dcdfe6; background: #fafafa; flex-wrap: wrap; }
.tbar-btn { width: 36px; height: 32px; border: 1px solid #d9d9d9; border-radius: 4px; background: #fff; cursor: pointer; font-size: 13px; display: flex; align-items: center; justify-content: center; }
.tbar-btn.active { background: #e6f7ff; border-color: #409eff; color: #409eff; }
.tbar-btn:hover { border-color: #409eff; color: #409eff; }
.tiptap-content { padding: 12px 16px; min-height: 240px; max-height: 480px; overflow-y: auto; }
.tiptap-content :deep(p) { margin: 0 0 8px; }
.tiptap-content :deep(h2) { margin: 12px 0 8px; font-size: 18px; }
.tiptap-content :deep(img) { max-width: 100%; }
</style>
