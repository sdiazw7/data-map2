import { useEffect, useRef } from 'react'

type Props = {
  message: string
  onDismiss: () => void
  /** How long to stay up. Conflicts reload the grid underneath, so they are given longer to read. */
  durationMs?: number
}

/**
 * Reports a failure that has already been undone. The grid keeps working behind it, so this
 * cannot be a blocking dialog — but a silently dropped edit is worse, since the user walks away
 * believing it saved.
 */
export default function ErrorToast({ message, onDismiss, durationMs = 6000 }: Props) {
  // Held in a ref so a caller passing an inline callback does not restart the timer on every
  // render of the page behind the toast.
  const dismissRef = useRef(onDismiss)
  dismissRef.current = onDismiss

  useEffect(() => {
    const timer = setTimeout(() => dismissRef.current(), durationMs)
    return () => clearTimeout(timer)
  }, [message, durationMs])

  return (
    <div
      role="alert"
      aria-live="assertive"
      className="fixed bottom-4 right-4 z-50 flex max-w-md items-start gap-3 rounded border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-700 shadow-lg"
    >
      <span className="flex-1">{message}</span>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="Dismiss"
        className="text-red-400 hover:text-red-600 focus:outline-none"
      >
        &times;
      </button>
    </div>
  )
}
