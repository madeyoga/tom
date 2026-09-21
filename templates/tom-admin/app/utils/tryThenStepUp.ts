/**
 * Try a sensitive action first. Only prompt when `isChallenge` says the
 * failure needs a step-up. A second challenge is not retried.
 */
export async function tryThenStepUp<T>(
  action: () => Promise<T>,
  prompt: () => Promise<boolean>,
  isChallenge: (error: unknown) => boolean
): Promise<T | undefined> {
  try {
    return await action()
  } catch (error) {
    if (!isChallenge(error)) {
      throw error
    }

    const confirmed = await prompt()
    if (!confirmed) {
      return undefined
    }

    return await action()
  }
}
