import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { useInvite } from '../../hooks/useInvite'
import { useSession } from '../../hooks/useSession'
import { joinInvite } from '../../services/inviteService'
import LoadingSpinner from '../ui/LoadingSpinner'
import ErrorMessage from '../ui/ErrorMessage'

export default function InvitePage() {
  const { token } = useParams<{ token: string }>()
  const { invite, isLoading, error } = useInvite(token ?? '')
  const { saveSession } = useSession()
  const navigate = useNavigate()

  const [email, setEmail] = useState('')
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    if (!email.trim()) {
      setSubmitError('Email is required.')
      return
    }
    if (!token) return

    setIsSubmitting(true)
    setSubmitError(null)
    try {
      const result = await joinInvite(token, { email: email.trim() })
      saveSession(result)
      navigate('/workspace')
    } catch (err: unknown) {
      setSubmitError(err instanceof Error ? err.message : 'Failed to join. Please try again.')
    } finally {
      setIsSubmitting(false)
    }
  }

  if (isLoading) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <LoadingSpinner />
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <ErrorMessage message={error} />
      </div>
    )
  }

  if (invite && !invite.isValid) {
    return (
      <div className="flex items-center justify-center min-h-screen">
        <p className="text-gray-600">This invite is no longer valid.</p>
      </div>
    )
  }

  if (!invite) {
    return null
  }

  return (
    <div className="flex items-center justify-center min-h-screen bg-gray-50">
      <div className="bg-white shadow rounded-lg p-8 w-full max-w-md">
        <h1 className="text-xl font-semibold text-gray-900 mb-1">
          Join {invite.workspaceName}
        </h1>
        <p className="text-sm text-gray-500 mb-6">Enter your email to get started.</p>
        <form onSubmit={handleSubmit} noValidate>
          <div className="mb-4">
            <label htmlFor="email" className="block text-sm font-medium text-gray-700 mb-1">
              Email address
            </label>
            <input
              id="email"
              type="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              placeholder="you@example.com"
              disabled={isSubmitting}
              className="w-full px-3 py-2 border border-gray-300 rounded text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
            />
          </div>
          {submitError && <ErrorMessage message={submitError} />}
          <button
            type="submit"
            disabled={isSubmitting}
            className="mt-4 w-full px-4 py-2 bg-blue-600 text-white text-sm font-medium rounded hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
          >
            {isSubmitting ? 'Joining...' : 'Join workspace'}
          </button>
        </form>
      </div>
    </div>
  )
}
