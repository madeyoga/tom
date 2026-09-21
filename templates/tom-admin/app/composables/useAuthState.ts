export function useAuthState() {
  const email = useState<string | null>('auth-email', () => null)

  return {
    email
  }
}
