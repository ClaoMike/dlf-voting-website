import { useCallback, useEffect, useState } from 'react'
import type { MyVote, VotingOption } from './types'

const OPTIONS_API = '/api/voting-options'
const VOTES_API = '/api/votes'
const STATUS_API = '/api/settings/voting'

export type SubmitProblem = { message: string; sessionExpired?: boolean }

export function useVotingData() {
    const [options, setOptions] = useState<VotingOption[]>([])
    const [myVote, setMyVote] = useState<MyVote | null>(null)
    const [isLoading, setIsLoading] = useState(true)
    const [loadError, setLoadError] = useState<string | null>(null)
    const [submitProblem, setSubmitProblem] = useState<SubmitProblem | null>(null)
    const [isSubmitting, setIsSubmitting] = useState(false)
    const [isVotingOpen, setIsVotingOpen] = useState<boolean | null>(null)

    const loadOptions = useCallback(async () => {
        const res = await fetch(OPTIONS_API, { credentials: 'include' })
        if (res.ok) setOptions(await res.json())
    }, [])

    useEffect(() => {
        const fetchAll = async () => {
            setIsLoading(true)
            setLoadError(null)
            try {
                const [statusRes, optionsRes, voteRes] = await Promise.all([
                    fetch(STATUS_API, { credentials: 'include' }),
                    fetch(OPTIONS_API, { credentials: 'include' }),
                    fetch(`${VOTES_API}/me`, { credentials: 'include' }),
                ])

                if (statusRes.ok) {
                    const statusData = await statusRes.json()
                    setIsVotingOpen(statusData.isVotingOpen)
                }

                // While voting is closed these two answer 403, which the closed message already covers.
                if (optionsRes.ok) setOptions(await optionsRes.json())
                if (voteRes.ok) setMyVote(await voteRes.json())
            } catch {
                setLoadError('Could not load the ballot. Please check your connection and reload the page.')
            } finally {
                setIsLoading(false)
            }
        }

        void fetchAll()
    }, [])

    const submitVote = async (optionId: string) => {
        setSubmitProblem(null)
        setIsSubmitting(true)
        try {
            const res = await fetch(VOTES_API, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ votingOptionId: optionId }),
            })

            if (res.ok) {
                setMyVote(await res.json())
                return true
            }

            if (res.status === 401) {
                setSubmitProblem({ message: 'Your session has expired, so your vote was not saved.', sessionExpired: true })
            } else if (res.status === 403) {
                setIsVotingOpen(false)
            } else if (res.status === 404) {
                setSubmitProblem({ message: 'That option is no longer on the ballot. Please choose again.' })
                await loadOptions()
            } else {
                setSubmitProblem({ message: 'Your vote could not be saved. Please try again.' })
            }
            return false
        } catch {
            setSubmitProblem({ message: 'Your vote could not be saved. Please check your connection and try again.' })
            return false
        } finally {
            setIsSubmitting(false)
        }
    }

    return { options, myVote, isLoading, loadError, submitProblem, isSubmitting, isVotingOpen, submitVote }
}
