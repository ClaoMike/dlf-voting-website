import { useState } from 'react'
import type { VoteRow } from './types'

const VOTES_API = 'http://localhost:5120/api/votes'

export function useVoteActions(onChanged: () => Promise<void>) {
    const [editingVote, setEditingVote] = useState<VoteRow | null>(null)
    const [deletingVote, setDeletingVote] = useState<VoteRow | null>(null)
    const [error, setError] = useState<string | null>(null)

    const confirmEdit = async (optionId: string) => {
        if (!editingVote) return
        setError(null)
        try {
            const res = await fetch(`${VOTES_API}/${editingVote.userId}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ votingOptionId: optionId }),
            })
            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to update vote.')
            }
            setEditingVote(null)
            await onChanged()
        } catch {
            setError('Failed to update vote.')
            setEditingVote(null)
        }
    }

    const confirmDelete = async () => {
        if (!deletingVote) return
        setError(null)
        try {
            const res = await fetch(`${VOTES_API}/${deletingVote.userId}`, {
                method: 'DELETE',
                credentials: 'include',
            })
            if (!res.ok && res.status !== 404) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to remove vote.')
            }
            setDeletingVote(null)
            await onChanged()
        } catch {
            setError('Failed to remove vote.')
            setDeletingVote(null)
        }
    }

    return {
        editingVote,
        deletingVote,
        error,
        startEdit: setEditingVote,
        startDelete: setDeletingVote,
        cancelEdit: () => setEditingVote(null),
        cancelDelete: () => setDeletingVote(null),
        confirmEdit,
        confirmDelete,
    }
}