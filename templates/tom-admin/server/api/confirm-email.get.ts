import { resolveServerApiBase } from '#shared/utils/apiBase'

function confirmStatusFromLocation(location: string): 'confirmed' | 'failed' | null {
  if (location.includes('status=confirmed')) {
    return 'confirmed'
  }
  if (location.includes('status=failed')) {
    return 'failed'
  }
  return null
}

export default defineEventHandler(async (event) => {
  const query = getQuery(event)
  const userId = typeof query.userId === 'string' ? query.userId : ''
  const code = typeof query.code === 'string' ? query.code : ''
  const changedEmail = typeof query.changedEmail === 'string' ? query.changedEmail : ''
  const flow = changedEmail ? 'change-email' : 'confirm'

  if (!userId || !code) {
    throw createError({
      statusCode: 400,
      statusMessage: 'userId and code are required'
    })
  }

  const config = useRuntimeConfig()
  const base = resolveServerApiBase(config.apiInternal, config.public.apiBase)
  if (!base) {
    throw createError({
      statusCode: 500,
      statusMessage: 'Set NUXT_API_INTERNAL or an absolute NUXT_PUBLIC_API_BASE for confirm-email'
    })
  }
  const url = new URL('/identity/confirmEmail', `${base}/`)
  url.searchParams.set('userId', userId)
  url.searchParams.set('code', code)
  if (changedEmail) {
    url.searchParams.set('changedEmail', changedEmail)
  }

  const response = await fetch(url, {
    method: 'GET',
    redirect: 'manual'
  })

  let location = response.headers.get('location') ?? ''
  let status = confirmStatusFromLocation(location)

  if (!status && location) {
    const next = new URL(location, url)
    if (next.origin === new URL(base).origin) {
      const hop = await fetch(next, {
        method: 'GET',
        redirect: 'manual'
      })
      location = hop.headers.get('location') ?? location
      status = confirmStatusFromLocation(location)
    }
  }

  return {
    status: status ?? (response.status === 200 ? 'confirmed' : 'failed'),
    flow,
    location: location || url.toString()
  }
})
