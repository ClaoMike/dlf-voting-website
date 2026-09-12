import { useState } from 'react'
import type { Administrator } from './types'

const API_BASE = 'http://localhost:5120/api/administrators'

export function useAdministratorActions(onChanged: (page: number) => Promise<void>, currentPage: number) {
    const [showCreate, setShowCreate] = useState(false)
    const [createError, setCreateError] = useState<string | null>(null)

    const [editingAdmin, setEditingAdmin] = useState<Administrator | null>(null)
    const [editError, setEditError] = useState<string | null>(null)

    const [deletingAdmin, setDeletingAdmin] = useState<Administrator | null>(null)
    const [deleteError, setDeleteError] = useState<string | null>(null)

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
                setCreateError(body?.message ?? 'Failed to create administrator.')
                return
            }

            setShowCreate(false)
            setRevealPassword({ email, password })
            await onChanged(1)
        } catch {
            setCreateError('Failed to create administrator.')
        }
    }

    const saveEdit = async (newEmail: string | null, newPassword: string | null) => {
        if (!editingAdmin) return

        setEditError(null)
        try {
            const res = await fetch(`${API_BASE}/${editingAdmin.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ email: newEmail, password: newPassword }),
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setEditError(body?.message ?? 'Failed to update administrator.')
                return
            }

            const finalEmail = newEmail ?? editingAdmin.email
            setEditingAdmin(null)
            await onChanged(currentPage)

            if (newPassword) {
                setRevealPassword({ email: finalEmail, password: newPassword })
            }
        } catch {
            setEditError('Failed to update administrator.')
        }
    }

    const confirmDelete = async () => {
        if (!deletingAdmin) return

        setDeleteError(null)
        try {
            const res = await fetch(`${API_BASE}/${deletingAdmin.id}`, {
                method: 'DELETE',
                credentials: 'include',
            })

            if (!res.ok && res.status !== 404) {
                const body = await res.json().catch(() => null)
                setDeleteError(body?.message ?? 'Failed to remove administrator.')
            }

            setDeletingAdmin(null)
            await onChanged(currentPage)
        } catch {
            setDeleteError('Failed to remove administrator.')
            setDeletingAdmin(null)
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

        editingAdmin,
        editError,
        startEdit: setEditingAdmin,
        cancelEdit: () => {
            setEditingAdmin(null)
            setEditError(null)
        },
        saveEdit,

        deletingAdmin,
        deleteError,
        startDelete: setDeletingAdmin,
        cancelDelete: () => setDeletingAdmin(null),
        confirmDelete,

        revealPassword,
        closeReveal: () => setRevealPassword(null),
    }
}