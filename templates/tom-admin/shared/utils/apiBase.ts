const SAME_ORIGIN = new Set(['', '.', '/', './'])

export function isSameOriginApiBase(value: unknown) {
  return SAME_ORIGIN.has(String(value ?? '').trim())
}

/** Blank, ".", and "/" are same-origin: callers use relative URLs. */
export function normalizeApiBase(value: unknown) {
  if (isSameOriginApiBase(value)) {
    return ''
  }
  return String(value).trim().replace(/\/+$/, '')
}

/**
 * Absolute base for the confirm-email server fetch.
 * Prefers NUXT_API_INTERNAL. Falls back to the public base only when that
 * value is an absolute URL, never a same-origin sentinel.
 */
export function resolveServerApiBase(apiInternal: unknown, publicApiBase: unknown) {
  const internal = String(apiInternal ?? '').trim().replace(/\/+$/, '')
  if (internal && !isSameOriginApiBase(internal)) {
    return internal
  }
  return normalizeApiBase(publicApiBase)
}
