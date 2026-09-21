/**
 * Minimal WebAuthn helpers for ASP.NET Identity passkey JSON options.
 * Options from the API use base64url strings; the browser needs ArrayBuffers.
 */

function base64UrlToBuffer(value: string): ArrayBuffer {
  const pad = '='.repeat((4 - (value.length % 4)) % 4)
  const base64 = (value + pad).replace(/-/g, '+').replace(/_/g, '/')
  const binary = atob(base64)
  const bytes = new Uint8Array(binary.length)
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i)
  }
  return bytes.buffer
}

function bufferToBase64Url(buffer: ArrayBuffer): string {
  const bytes = new Uint8Array(buffer)
  let binary = ''
  for (const b of bytes) {
    binary += String.fromCharCode(b)
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

function reviveCreationOptions(options: PublicKeyCredentialCreationOptionsJSON): PublicKeyCredentialCreationOptions {
  return {
    ...options,
    challenge: base64UrlToBuffer(options.challenge),
    user: {
      ...options.user,
      id: base64UrlToBuffer(options.user.id)
    },
    excludeCredentials: options.excludeCredentials?.map(cred => ({
      ...cred,
      id: base64UrlToBuffer(cred.id),
      type: cred.type ?? 'public-key'
    }))
  } as PublicKeyCredentialCreationOptions
}

function reviveRequestOptions(options: PublicKeyCredentialRequestOptionsJSON): PublicKeyCredentialRequestOptions {
  return {
    ...options,
    challenge: base64UrlToBuffer(options.challenge),
    allowCredentials: options.allowCredentials?.map(cred => ({
      ...cred,
      id: base64UrlToBuffer(cred.id),
      type: cred.type ?? 'public-key'
    }))
  } as PublicKeyCredentialRequestOptions
}

function serializeCredential(credential: PublicKeyCredential): string {
  const response = credential.response

  if (response instanceof AuthenticatorAttestationResponse) {
    return JSON.stringify({
      id: credential.id,
      rawId: bufferToBase64Url(credential.rawId),
      type: credential.type,
      response: {
        clientDataJSON: bufferToBase64Url(response.clientDataJSON),
        attestationObject: bufferToBase64Url(response.attestationObject),
        transports: response.getTransports?.() ?? []
      },
      clientExtensionResults: credential.getClientExtensionResults()
    })
  }

  if (response instanceof AuthenticatorAssertionResponse) {
    return JSON.stringify({
      id: credential.id,
      rawId: bufferToBase64Url(credential.rawId),
      type: credential.type,
      response: {
        clientDataJSON: bufferToBase64Url(response.clientDataJSON),
        authenticatorData: bufferToBase64Url(response.authenticatorData),
        signature: bufferToBase64Url(response.signature),
        userHandle: response.userHandle ? bufferToBase64Url(response.userHandle) : null
      },
      clientExtensionResults: credential.getClientExtensionResults()
    })
  }

  throw new Error('Unsupported credential response type')
}

export async function createPasskeyCredential(optionsJson: string | object): Promise<string> {
  const options = (typeof optionsJson === 'string'
    ? JSON.parse(optionsJson)
    : optionsJson) as PublicKeyCredentialCreationOptionsJSON
  const publicKey = reviveCreationOptions(options)
  const credential = await navigator.credentials.create({ publicKey }) as PublicKeyCredential | null
  if (!credential) {
    throw new Error('Passkey creation was cancelled or failed')
  }
  return serializeCredential(credential)
}

export async function getPasskeyCredential(optionsJson: string | object): Promise<string> {
  const options = (typeof optionsJson === 'string'
    ? JSON.parse(optionsJson)
    : optionsJson) as PublicKeyCredentialRequestOptionsJSON
  const publicKey = reviveRequestOptions(options)
  const credential = await navigator.credentials.get({ publicKey }) as PublicKeyCredential | null
  if (!credential) {
    throw new Error('Passkey assertion was cancelled or failed')
  }
  return serializeCredential(credential)
}

/** Subset of the WebAuthn Level 3 JSON option shapes returned by ASP.NET Identity. */
interface PublicKeyCredentialCreationOptionsJSON {
  challenge: string
  user: { id: string, name: string, displayName: string }
  excludeCredentials?: { id: string, type?: PublicKeyCredentialType, transports?: AuthenticatorTransport[] }[]
  rp: PublicKeyCredentialRpEntity
  pubKeyCredParams: PublicKeyCredentialParameters[]
  timeout?: number
  attestation?: AttestationConveyancePreference
  authenticatorSelection?: AuthenticatorSelectionCriteria
}

interface PublicKeyCredentialRequestOptionsJSON {
  challenge: string
  allowCredentials?: { id: string, type?: PublicKeyCredentialType, transports?: AuthenticatorTransport[] }[]
  timeout?: number
  rpId?: string
  userVerification?: UserVerificationRequirement
}
