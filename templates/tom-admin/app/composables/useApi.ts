import { normalizeApiBase } from '#shared/utils/apiBase'

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

export const CSRF_PATH = '/identity/csrfToken'

interface ApiRequestOptions {
  method?: HttpMethod
  body?: unknown
  query?: Record<string, string | number | boolean | undefined | null>
  /** Skip CSRF for this call */
  skipCsrf?: boolean
  /** Attach X-AuthEndpoints-Reauth */
  reauth?: boolean
  /** Override the stored ReAuth token for this call (empty skips the header so the ReAuth cookie can be used). */
  reauthToken?: string | null
}

function isMutating(method: HttpMethod) {
  return method !== 'GET'
}

export function useApi() {
  const config = useRuntimeConfig()

  const baseURL = computed(() => normalizeApiBase(config.public.apiBase))

  const csrfCache = useState<{ cookie?: string }>('auth-csrf-cache', () => ({}))

  const fetchCsrf = async () => {
    const response = await $fetch<{ csrfToken?: string, CsrfToken?: string }>(CSRF_PATH, {
      baseURL: baseURL.value,
      credentials: 'include',
      method: 'GET'
    })
    const token = response.csrfToken ?? response.CsrfToken ?? ''
    csrfCache.value = { cookie: token }
    return token
  }

  const ensureCsrf = async () => {
    if (csrfCache.value.cookie) {
      return csrfCache.value.cookie
    }
    return fetchCsrf()
  }

  const api = async <T = unknown>(path: string, options: ApiRequestOptions = {}): Promise<T> => {
    const method = (options.method ?? 'GET').toUpperCase() as HttpMethod
    const url = `${baseURL.value}${path.startsWith('/') ? path : `/${path}`}`
    const headers: Record<string, string> = {
      Accept: 'application/json'
    }

    if (options.body !== undefined) {
      headers['Content-Type'] = 'application/json'
    }

    if (options.reauth) {
      const token = options.reauthToken !== undefined ? options.reauthToken : null
      if (token) {
        headers['X-AuthEndpoints-Reauth'] = token
      }
    }

    if (isMutating(method) && !options.skipCsrf) {
      const csrf = await ensureCsrf()
      headers.RequestVerificationToken = csrf
    }

    return await $fetch<T>(url, {
      method,
      credentials: 'include',
      headers,
      body: options.body as BodyInit | Record<string, unknown> | null | undefined,
      query: options.query
    }) as T
  }

  const clearCsrf = () => {
    csrfCache.value = {}
  }

  return {
    baseURL,
    api,
    fetchCsrf,
    ensureCsrf,
    clearCsrf
  }
}
