import type { ExternalLogin, ManageInfo, PasskeyCredential, TwoFactorStatus } from '~/types/auth'

export function normalizeInfoResponse(raw: unknown): ManageInfo {
  const value = (raw ?? {}) as Record<string, unknown>
  const claims = value.claims ?? value.Claims
  return {
    email: String(value.email ?? value.Email ?? ''),
    isEmailConfirmed: Boolean(value.isEmailConfirmed ?? value.IsEmailConfirmed),
    claims: Array.isArray(claims)
      ? claims.map((claim) => {
          const item = claim as Record<string, unknown>
          return {
            type: String(item.type ?? item.Type ?? ''),
            value: String(item.value ?? item.Value ?? '')
          }
        })
      : undefined
  }
}

export function normalizeTwoFactorResponse(raw: unknown): TwoFactorStatus {
  const value = (raw ?? {}) as Record<string, unknown>
  const codes = value.recoveryCodes ?? value.RecoveryCodes
  const left = value.recoveryCodesLeft ?? value.RecoveryCodesLeft

  return {
    sharedKey: String(value.sharedKey ?? value.SharedKey ?? ''),
    recoveryCodesLeft: typeof left === 'number' ? left : undefined,
    recoveryCodes: Array.isArray(codes) ? codes.map(String) : null,
    isTwoFactorEnabled: Boolean(value.isTwoFactorEnabled ?? value.IsTwoFactorEnabled),
    isMachineRemembered: Boolean(value.isMachineRemembered ?? value.IsMachineRemembered)
  }
}

export function totpUri(email: string, sharedKey: string) {
  const issuer = encodeURIComponent('Tom Admin')
  const account = encodeURIComponent(email || 'account')
  return `otpauth://totp/${issuer}:${account}?secret=${encodeURIComponent(sharedKey)}&issuer=${issuer}&digits=6`
}

export function readPasskeys(raw: unknown): PasskeyCredential[] {
  const value = (raw ?? {}) as Record<string, unknown>
  const list = value.passkeys ?? value.Passkeys
  return Array.isArray(list) ? list as PasskeyCredential[] : []
}

export function normalizeExternalLogins(raw: unknown): ExternalLogin[] {
  const list = Array.isArray(raw) ? raw : []
  return list.map((item) => {
    const value = (item ?? {}) as Record<string, unknown>
    const displayName = value.providerDisplayName ?? value.ProviderDisplayName
    return {
      loginProvider: String(value.loginProvider ?? value.LoginProvider ?? ''),
      providerKey: String(value.providerKey ?? value.ProviderKey ?? ''),
      providerDisplayName: displayName == null || displayName === '' ? null : String(displayName)
    }
  }).filter(item => item.loginProvider && item.providerKey)
}
