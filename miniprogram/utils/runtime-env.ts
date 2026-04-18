export const APP_ENV = import.meta.env.VITE_APP_ENV || 'development'

export const USE_MOCK = import.meta.env.VITE_USE_MOCK === 'true'

export const USE_REAL_LOCATION = import.meta.env.VITE_USE_REAL_LOCATION === 'true'

export const AUTO_LOCATE_ON_INIT =
  import.meta.env.VITE_AUTO_LOCATE_ON_INIT === 'true'

export const IS_DEV_LIKE = APP_ENV === 'development' || APP_ENV === 'preview'
