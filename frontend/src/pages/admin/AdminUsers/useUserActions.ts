import { useState } from 'react'
import type { User } from './types'

const API_BASE = 'http://localhost:5120/api/users'

export function useUserActions(onChanged: (page: number) => Promise<void>, currentPage: number) {
    const [showCreate, setShowCreate] = useState(false)
    const [createError, setCreateError] = useState<string | null>(null)

    const [editingUser, setEditingUser] = useState<User | null>(null)
    const [editError, setEditError] = useState<string | null>(null)

    const [deletingUser, setDeletingUser] = useState<User | null>(null)
    const [showRemoveAllConfirm, setShowRemoveAllConfirm] = useState(false)

    const [removeAllError, setRemoveAllError] = useState<string | null>(null)

    const [revealPassword, setRevealPassword] = useState<{ email: string; password: string } | null>(null)

    const create = async (email: string, password: string) => {
        setCreateError(null)
        try {
            const res = await fetch(API_BASE, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ email, password }),
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setCreateError(body?.message ?? 'Failed to create user.')
                return
            }

            setShowCreate(false)
            setRevealPassword({ email, password })
            await onChanged(1)
        } catch {
            setCreateError('Failed to create user.')
        }
    }

    const saveEdit = async (newEmail: string | null, newPassword: string | null) => {
        if (!editingUser) return

        setEditError(null)
        try {
            const res = await fetch(`${API_BASE}/${editingUser.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ email: newEmail, password: newPassword }),
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setEditError(body?.message ?? 'Failed to update user.')
                return
            }

            const finalEmail = newEmail ?? editingUser.email
            setEditingUser(null)
            await onChanged(currentPage)

            if (newPassword) {
                setRevealPassword({ email: finalEmail, password: newPassword })
            }
        } catch {
            setEditError('Failed to update user.')
        }
    }

    const confirmDelete = async () => {
        if (!deletingUser) return

        try {
            const res = await fetch(`${API_BASE}/${deletingUser.id}`, {
                method: 'DELETE',
                credentials: 'include',
            })

            if (!res.ok && res.status !== 404) {
                const body = await res.json().catch(() => null)
                setRemoveAllError(body?.message ?? 'Failed to delete user.')
            }

            setDeletingUser(null)
            await onChanged(currentPage)
        } catch {
            setRemoveAllError('Failed to delete user.')
            setDeletingUser(null)
        }
    }

    const confirmRemoveAll = async () => {
        setRemoveAllError(null)
        try {
            const res = await fetch(API_BASE, { method: 'DELETE', credentials: 'include' })
            if (!res.ok) setRemoveAllError('Failed to remove all users.')
            setShowRemoveAllConfirm(false)
            await onChanged(1)
        } catch {
            setRemoveAllError('Failed to remove all users.')
            setShowRemoveAllConfirm(false)
        }
    }

    return {
        showCreate,
        createError,
        openCreate: () => setShowCreate(true),
        cancelCreate: () => {
            setShowCreate(false)
            setCreateError(null)
        },
        create,

        editingUser,
        editError,
        startEdit: setEditingUser,
        cancelEdit: () => {
            setEditingUser(null)
            setEditError(null)
        },
        saveEdit,

        deletingUser,
        startDelete: setDeletingUser,
        cancelDelete: () => setDeletingUser(null),
        confirmDelete,

        showRemoveAllConfirm,
        openRemoveAllConfirm: () => setShowRemoveAllConfirm(true),
        cancelRemoveAllConfirm: () => setShowRemoveAllConfirm(false),
        confirmRemoveAll,

        removeAllError,

        revealPassword,
        closeReveal: () => setRevealPassword(null),
    }
}