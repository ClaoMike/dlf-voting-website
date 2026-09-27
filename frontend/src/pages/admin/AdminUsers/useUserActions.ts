import { useState } from 'react'
import type { User, UserListItem } from './types'

const API_BASE = 'http://localhost:5120/api/users'

export function useUserActions(onChanged: (page: number) => Promise<void>, currentPage: number) {
    const [showCreate, setShowCreate] = useState(false)
    const [createError, setCreateError] = useState<string | null>(null)

    const [editingUser, setEditingUser] = useState<User | null>(null)
    const [editError, setEditError] = useState<string | null>(null)

    const [selectedUser, setSelectedUser] = useState<User | null>(null)

    const [deletingUser, setDeletingUser] = useState<UserListItem | null>(null)
    const [loadError, setLoadError] = useState<string | null>(null)
    const [showRemoveAllConfirm, setShowRemoveAllConfirm] = useState(false)

    const [removeAllError, setRemoveAllError] = useState<string | null>(null)

    const [revealPassword, setRevealPassword] = useState<{ accountName: string; password: string } | null>(null)

    const create = async (username: string, email: string | null, password: string) => {
        setCreateError(null)
        try {
            const res = await fetch(API_BASE, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ username, email, password }),
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setCreateError(body?.message ?? 'Failed to create user.')
                return
            }

            setShowCreate(false)
            setRevealPassword({ accountName: username, password })
            await onChanged(1)
        } catch {
            setCreateError('Failed to create user.')
        }
    }

    // The table only has name and username, so details and edit load the full user first.
    const fetchUser = async (item: UserListItem): Promise<User | null> => {
        setLoadError(null)
        try {
            const res = await fetch(`${API_BASE}/${item.id}`, { credentials: 'include' })
            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setLoadError(body?.message ?? 'Could not load this user.')
                return null
            }
            return await res.json()
        } catch {
            setLoadError('Could not load this user.')
            return null
        }
    }

    const selectUser = async (item: UserListItem) => {
        const user = await fetchUser(item)
        if (user) setSelectedUser(user)
    }

    const startEdit = async (item: UserListItem) => {
        const user = await fetchUser(item)
        if (user) setEditingUser(user)
    }

    const saveEdit = async (newUsername: string, newEmail: string | null, newPassword: string | null) => {
        if (!editingUser) return

        setEditError(null)
        try {
            const res = await fetch(`${API_BASE}/${editingUser.id}`, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include',
                body: JSON.stringify({ username: newUsername, email: newEmail, password: newPassword }),
            })

            if (!res.ok) {
                const body = await res.json().catch(() => null)
                setEditError(body?.message ?? 'Failed to update user.')
                return
            }

            setEditingUser(null)
            await onChanged(currentPage)

            if (newPassword) {
                setRevealPassword({ accountName: newUsername, password: newPassword })
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
        startEdit,
        cancelEdit: () => {
            setEditingUser(null)
            setEditError(null)
        },
        saveEdit,

        selectedUser,
        selectUser,
        closeDetails: () => setSelectedUser(null),

        deletingUser,
        startDelete: setDeletingUser,
        cancelDelete: () => setDeletingUser(null),
        confirmDelete,

        showRemoveAllConfirm,
        openRemoveAllConfirm: () => setShowRemoveAllConfirm(true),
        cancelRemoveAllConfirm: () => setShowRemoveAllConfirm(false),
        confirmRemoveAll,

        removeAllError,
        loadError,

        revealPassword,
        closeReveal: () => setRevealPassword(null),
    }
}