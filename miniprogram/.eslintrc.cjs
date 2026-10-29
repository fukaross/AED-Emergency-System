// @ts-check

/** @type {import('eslint').Linter.Config} */
module.exports = {
  root: true,
  env: {
    browser: true,
    es2022: true,
    node: true,
  },
  parser: 'vue-eslint-parser',
  parserOptions: {
    parser: '@typescript-eslint/parser',
    ecmaVersion: 'latest',
    sourceType: 'module',
  },
  extends: [
    'eslint:recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:vue/vue3-recommended',
    'prettier',
  ],
  plugins: ['@typescript-eslint'],
  rules: {
    // Vue
    'vue/multi-word-component-names': 'off',
    'vue/no-setup-props-destructure': 'off',

    // TypeScript
    '@typescript-eslint/no-unused-vars': ['warn', { argsIgnorePattern: '^_' }],
    '@typescript-eslint/no-explicit-any': 'warn',
    '@typescript-eslint/consistent-type-imports': 'error',

    // G-05: 禁止直调 uni.request / uni.uploadFile
    // services/request.ts 是唯一允许调用的位置（通过 overrides 豁免）
    'no-restricted-properties': [
      'error',
      {
        object: 'uni',
        property: 'request',
        message: '[G-05] 禁止直调 uni.request，请通过 services/request.ts。',
      },
      {
        object: 'uni',
        property: 'uploadFile',
        message: '[G-05] 禁止直调 uni.uploadFile，请通过 services/request.ts。',
      },
    ],
  },
  overrides: [
    {
      // services/ 是基础设施层，允许直接调用 uni.request / uni.uploadFile
      files: ['services/**/*.ts'],
      rules: {
        'no-restricted-properties': 'off',
      },
    },
  ],
  globals: {
    uni: 'readonly',
    wx: 'readonly',
    getCurrentPages: 'readonly',
    UniApp: 'readonly',
  },
  ignorePatterns: [
    'dist/',
    'node_modules/',
    'unpackage/',
    '*.d.ts',
  ],
}
