import { useEffect, useState } from 'react'
import type { VotingOption } from './types'

const API_BASE = 'http://localhost:5120/api/voting-options'

export function useVotingOptionsData() {
    const [options, setOptions] = useState<VotingOption[]>([])
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const fetchOptions = async () => {
        setIsLoading(true)
        setError(null)
        try {
            const res = await fetch(API_BASE, { credentials: 'include' })
            if (!res.ok) throw new Error('Failed to load voting options.')
            const data = await res.json()
            setOptions(data)
        } catch {
            setError('Could not load voting options.')
        } finally {
            setIsLoading(false)
        }
    }

    useEffect(() => {
        fetchOptions()
    }, [])

    return { options, isLoading, error, fetchOptions }
}