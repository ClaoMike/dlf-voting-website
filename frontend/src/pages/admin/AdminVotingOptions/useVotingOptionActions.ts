import { useState } from 'react'
import type { VotingOption } from './types'

const API_BASE = 'http://localhost:5120/api/voting-options'

export function useVotingOptionActions(onChanged: () => Promise<void>) {
    const [newName, setNewName] = useState('')
    const [isAdding, setIsAdding] = useState(false)

    const [editingOption, setEditingOption] = useState<VotingOption | null>(null)
    const [deletingOption, setDeletingOption] = useState<VotingOption | null>(null)
    const [showRemoveAllConfirm, setShowRemoveAllConfirm] = useState(false)

    const [error, setError] = useState<string | null>(null)

    const add = async () => {
        const trimmed = newName.trim()
        if (!trimmed) return

        setIsAdding(true)
        setError(null)
        try {
            const res = await fetch(API_BASE, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ name: trimmed }),
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to add voting option.')
                return
            }

            setNewName('')
            await onChanged()
        } finally {
            setIsAdding(false)
        }
    }

    const saveEdit = async (newValue: string) => {
        if (!editingOption) return

        setError(null)
        try {
            const res = await fetch(`${API_BASE}/${editingOption.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ name: newValue }),
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to update voting option.')
                setEditingOption(null)
                return
            }

            setEditingOption(null)
            await onChanged()
        } catch {
            setError('Failed to update voting option.')
            setEditingOption(null)
        }
    }

    const confirmDelete = async () => {
        if (!deletingOption) return

        setError(null)
        try {
            const res = await fetch(`${API_BASE}/${deletingOption.id}`, {
                method: 'DELETE',
                credentials: 'include',
            })

            if (!res.ok && res.status !== 404) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to delete voting option.')
            }

            setDeletingOption(null)
            await onChanged()
        } catch {
            setError('Failed to delete voting option.')
            setDeletingOption(null)
        }
    }

    const confirmRemoveAll = async () => {
        setError(null)
        try {
            const res = await fetch(API_BASE, { method: 'DELETE', credentials: 'include' })

            if (!res.ok) {
                setError('Failed to remove all voting options.')
            }

            setShowRemoveAllConfirm(false)
            await onChanged()
        } catch {
            setError('Failed to remove all voting options.')
            setShowRemoveAllConfirm(false)
        }
    }

    return {
        newName,
        setNewName,
        isAdding,
        add,

        editingOption,
        startEdit: setEditingOption,
        cancelEdit: () => setEditingOption(null),
        saveEdit,

        deletingOption,
        startDelete: setDeletingOption,
        cancelDelete: () => setDeletingOption(null),
        confirmDelete,

        showRemoveAllConfirm,
        openRemoveAllConfirm: () => setShowRemoveAllConfirm(true),
        cancelRemoveAllConfirm: () => setShowRemoveAllConfirm(false),
        confirmRemoveAll,

        error,
    }
}