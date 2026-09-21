import { FetchError } from 'ofetch'
import type {
  AuthMethodsResponse,
  ConfirmIdentityBody,
  ConfirmIdentityResponse,
  StepUpMethod
} from '~/types/auth'

export function normalizeAuthMethods(raw: Record<string, unknown> | AuthMethodsResponse): AuthMethodsResponse {
  const value = raw as Record<string, unknown>
  return {
    password: Boolean(value.password ?? value.Password),
    authenticator: Boolean(value.authenticator ?? value.Authenticator),
    recoveryCodes: Boolean(value.recoveryCodes ?? value.RecoveryCodes),
    passkeys: Boolean(value.passkeys ?? value.Passkeys),
    passkeyCount: Number(value.passkeyCount ?? value.PasskeyCount ?? 0)
  }
}

export function visibleStepUpMethods(methods: AuthMethodsResponse): StepUpMethod[] {
  const available: StepUpMethod[] = []
  if (methods.password) {
    available.push('password')
  }
  if (methods.authenticator) {
    available.push('authenticator')
  }
  if (methods.recoveryCodes) {
    available.push('recoveryCodes')
  }
  if (methods.passkeys) {
    available.push('passkeys')
  }
  return available
}

export function buildConfirmIdentityBody(method: Exclude<StepUpMethod, 'passkeys'>, proof: string): ConfirmIdentityBody {
  if (method === 'password') {
    return { password: proof }
  }
  if (method === 'authenticator') {
    return { twoFactorCode: proof }
  }
  return { twoFactorRecoveryCode: proof }
}

export function fetchStatus(error: unknown): number | undefined {
  if (error instanceof FetchError) {
    const withStatus = error as FetchError & { status?: number }
    return withStatus.statusCode ?? withStatus.status ?? error.response?.status ?? undefined
  }
  return undefined
}

export function isReauthChallenge(error: unknown): boolean {
  const status = fetchStatus(error)
  return status === 401 || status === 403
}

export function readReauthToken(raw: Record<string, unknown> | ConfirmIdentityResponse): string {
  const value = raw as Record<string, unknown>
  const token = value.reauthToken ?? value.ReauthToken
  return typeof token === 'string' ? token : ''
}

export function reauthRequest(reauthToken: string) {
  return {
    reauth: true as const,
    reauthToken: reauthToken || undefined
  }
}
