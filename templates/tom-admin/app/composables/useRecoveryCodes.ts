export function useRecoveryCodes() {
  const toast = useToast()

  function copy(codes: string[] | null | undefined) {
    if (!codes || codes.length === 0) {
      toast.add({
        title: 'No recovery codes found',
        color: 'error'
      })
      return
    }

    navigator.clipboard.writeText(codes.join('\n'))
      .then(() => {
        toast.add({
          title: 'Recovery codes copied to clipboard',
          color: 'success'
        })
      })
      .catch(() => {
        toast.add({
          title: 'Unable to copy codes',
          description: 'Copy them manually.',
          color: 'error'
        })
      })
  }

  function download(codes: string[] | null | undefined) {
    if (!codes || codes.length === 0) {
      toast.add({
        title: 'No recovery codes found',
        color: 'error'
      })
      return
    }

    const blob = new Blob([codes.join('\n')], { type: 'text/plain' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'tom-admin-recovery-codes.txt'
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }

  return {
    copy,
    download
  }
}
