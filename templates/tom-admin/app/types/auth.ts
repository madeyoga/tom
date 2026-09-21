export interface ManageInfo {
  email?: string | null
  isEmailConfirmed?: boolean
  claims?: { type: string, value: string }[]
}

export interface PasskeyCredential {
  credentialId: string
  displayName?: string | null
  createdAt?: string | null
}

export interface TwoFactorStatus {
  sharedKey?: string
  recoveryCodesLeft?: number
  recoveryCodes?: string[] | null
  isTwoFactorEnabled: boolean
  isMachineRemembered?: boolean
}

export type StepUpMethod = 'password' | 'authenticator' | 'recoveryCodes' | 'passkeys'

export interface AuthMethodsResponse {
  password: boolean
  authenticator: boolean
  recoveryCodes: boolean
  passkeys: boolean
  passkeyCount: number
}

export interface ConfirmIdentityResponse {
  reauthToken: string
}

export interface ConfirmIdentityBody {
  password?: string
  twoFactorCode?: string
  twoFactorRecoveryCode?: string
  credentialJson?: string
}

export interface StepUpContext {
  reauthToken: string
}

export interface InfoUpdateRequest {
  newEmail?: string
  newPassword?: string
  oldPassword?: string
}

export interface ExternalLogin {
  loginProvider: string
  providerKey: string
  providerDisplayName?: string | null
}
