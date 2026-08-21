import { useEffect, useRef } from 'react'

export type ToastVariant = 'error' | 'notice'

type Props = {
  message: string
  onDismiss: () => void
  /** Errors are announced assertively; a notice reports something that merely did not apply. */
  variant?: ToastVariant
  /** How long to stay up. Conflicts reload the grid underneath, so they are given longer to read. */
  durationMs?: number
}

const VARIANTS: Record<ToastVariant, { box: string; close: string }> = {
  error: {
    box: 'border-red-300 bg-red-50 text-red-700',
    close: 'text-red-400 hover:text-red-600',
  },
  notice: {
    box: 'border-amber-300 bg-amber-50 text-amber-800',
    close: 'text-amber-500 hover:text-amber-700',
  },
}

/**
 * Reports something that has already happened — a failure that was undone, or a change that
 * did not apply. The grid keeps working behind it, so this cannot be a blocking dialog. But a
 * silently dropped edit is worse, since the user walks away believing it saved.
 */
export default function Toast({
  message,
  onDismiss,
  variant = 'error',
  durationMs = 6000,
}: Props) {
  // Held in a ref so a caller passing an inline callback does not restart the timer on every
  // render of the page behind the toast.
  const dismissRef = useRef(onDismiss)
  dismissRef.current = onDismiss

  useEffect(() => {
    const timer = setTimeout(() => dismissRef.current(), durationMs)
    return () => clearTimeout(timer)
  }, [message, durationMs])

  const styles = VARIANTS[variant]

  return (
    <div
      role={variant === 'error' ? 'alert' : 'status'}
      aria-live={variant === 'error' ? 'assertive' : 'polite'}
      className={`fixed bottom-4 right-4 z-50 flex max-w-md items-start gap-3 rounded border px-4 py-3 text-sm shadow-lg ${styles.box}`}
    >
      <span className="flex-1">{message}</span>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="Dismiss"
        className={`focus:outline-none ${styles.close}`}
      >
        &times;
      </button>
    </div>
  )
}
