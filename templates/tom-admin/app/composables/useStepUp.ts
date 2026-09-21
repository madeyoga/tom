import type { StepUpContext } from '~/types/auth'

/** In-memory only — never persist the ReAuth token or pending actions. */
let lastReauthToken: string | undefined
let pendingResolve: ((token: string | undefined) => void) | undefined

export function useStepUpUi() {
  const open = useState('step-up-open', () => false)

  function finish(result?: { reauthToken: string }) {
    open.value = false
    const resolve = pendingResolve
    pendingResolve = undefined
    if (!result || typeof result !== 'object' || !('reauthToken' in result)) {
      resolve?.(undefined)
      return
    }
    lastReauthToken = result.reauthToken || undefined
    resolve?.(lastReauthToken ?? '')
  }

  return {
    open,
    finish
  }
}

export function useStepUp() {
  const { open } = useStepUpUi()

  async function prompt(): Promise<string | undefined> {
    return await new Promise((resolve) => {
      pendingResolve = resolve
      open.value = true
    })
  }

  async function invoke<T>(action: (ctx: StepUpContext) => Promise<T>): Promise<T> {
    const ctx: StepUpContext = {
      reauthToken: lastReauthToken ?? ''
    }
    return await action(ctx)
  }

  async function run<T>(action: (ctx: StepUpContext) => Promise<T>): Promise<T | undefined> {
    // Try first so a still-valid HttpOnly ReAuth cookie can succeed without a prompt.
    return await tryThenStepUp(
      () => invoke(action),
      async () => {
        lastReauthToken = undefined
        const token = await prompt()
        return token !== undefined
      },
      isReauthChallenge
    )
  }

  return {
    run
  }
}
