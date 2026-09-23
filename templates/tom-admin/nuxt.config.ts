// https://nuxt.com/docs/api/configuration/nuxt-config
export default defineNuxtConfig({
  modules: [
    '@nuxt/eslint',
    '@nuxt/ui'
  ],

  devtools: {
    enabled: true
  },

  telemetry: false,

  css: ['~/assets/css/main.css'],

  runtimeConfig: {
    // Server-only. NUXT_API_INTERNAL. Confirm-email falls back to public.apiBase
    // only when that value is an absolute URL.
    apiInternal: '',
    public: {
      // Local dual-port default. Blank, ".", and "/" mean same-origin.
      apiBase: 'http://localhost:5080'
    }
  },

  compatibilityDate: '2026-06-30',

  vite: {
    optimizeDeps: {
      include: ['qrcode']
    }
  },

  eslint: {
    config: {
      stylistic: {
        commaDangle: 'never',
        braceStyle: '1tbs'
      }
    }
  }
})
