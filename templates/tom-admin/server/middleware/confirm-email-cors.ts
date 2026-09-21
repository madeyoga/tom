const CONFIRM_EMAIL_PATH = '/confirm-email'

export default defineEventHandler((event) => {
  if (!event.path.startsWith(CONFIRM_EMAIL_PATH)) {
    return
  }

  const origin = getRequestHeader(event, 'origin')
  if (!origin) {
    return
  }

  setHeader(event, 'Access-Control-Allow-Origin', origin)
  setHeader(event, 'Access-Control-Allow-Credentials', 'true')
  setHeader(event, 'Vary', 'Origin')

  if (event.method === 'OPTIONS') {
    setHeader(event, 'Access-Control-Allow-Methods', 'GET,OPTIONS')
    setHeader(event, 'Access-Control-Allow-Headers', 'Content-Type')
    setResponseStatus(event, 204)
    return ''
  }
})
