import { useEffect, useState } from 'react'
import ConfirmDialog from '../../components/ConfirmDialog'
import CreateUserDialog from '../../components/CreateUserDialog'
import EditUserDialog from '../../components/EditUserDialog'
import PasswordRevealDialog from '../../components/PasswordRevealDialog'
import '../admin/AdminVotingOptions.css'
import './AdminUsers.css'

type User = {
    id: string
    email: string
    createdAt: string
}

type PagedUsers = {
    items: User[]
    totalCount: number
    page: number
    pageSize: number
}

const API_BASE = 'http://localhost:5120/api/users'

function AdminUsers() {
    const [users, setUsers] = useState<User[]>([])
    const [page, setPage] = useState(1)
    const [totalCount, setTotalCount] = useState(0)
    const [pageSize, setPageSize] = useState(25)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const [showCreate, setShowCreate] = useState(false)
    const [createError, setCreateError] = useState<string | null>(null)

    const [editingUser, setEditingUser] = useState<User | null>(null)
    const [editError, setEditError] = useState<string | null>(null)

    const [deletingUser, setDeletingUser] = useState<User | null>(null)
    const [showRemoveAllConfirm, setShowRemoveAllConfirm] = useState(false)

    const [revealPassword, setRevealPassword] = useState<{ email: string; password: string } | null>(null)

    const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1)

    const fetchUsers = async (targetPage: number) => {
        setIsLoading(true)
        setError(null)
        try {
            const res = await fetch(`${API_BASE}?page=${targetPage}`, { credentials: 'include' })
            if (!res.ok) throw new Error('Failed to load users.')
            const data: PagedUsers = await res.json()
            setUsers(data.items)
            setTotalCount(data.totalCount)
            setPageSize(data.pageSize)
            setPage(data.page)
        } catch {
            setError('Could not load users.')
        } finally {
            setIsLoading(false)
        }
    }

    useEffect(() => {
        fetchUsers(1)
    }, [])

    const handleCreate = async (email: string, password: string) => {
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
            await fetchUsers(1)
        } catch {
            setCreateError('Failed to create user.')
        }
    }

    const handleEditSave = async (newEmail: string | null, newPassword: string | null) => {
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
            await fetchUsers(page)

            if (newPassword) {
                setRevealPassword({ email: finalEmail, password: newPassword })
            }
        } catch {
            setEditError('Failed to update user.')
        }
    }

    const handleConfirmDelete = async () => {
        if (!deletingUser) return

        setError(null)
        try {
            const res = await fetch(`${API_BASE}/${deletingUser.id}`, {
                method: 'DELETE',
                credentials: 'include',
            })

            if (!res.ok && res.status !== 404) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to delete user.')
            }

            setDeletingUser(null)
            await fetchUsers(page)
        } catch {
            setError('Failed to delete user.')
            setDeletingUser(null)
        }
    }

    const handleConfirmRemoveAll = async () => {
        setError(null)
        try {
            const res = await fetch(API_BASE, { method: 'DELETE', credentials: 'include' })
            if (!res.ok) setError('Failed to remove all users.')
            setShowRemoveAllConfirm(false)
            await fetchUsers(1)
        } catch {
            setError('Failed to remove all users.')
            setShowRemoveAllConfirm(false)
        }
    }

    return (
        <div className="voting-options-page">
            <h1>Users</h1>

            <div className="users-actions-row">
                <button className="voting-options-add-row-button" onClick={() => setShowCreate(true)}>
                    Add user
                </button>

                <button
                    className="voting-options-remove-all"
                    disabled={users.length === 0}
                    onClick={() => setShowRemoveAllConfirm(true)}
                >
                    Remove all
                </button>
            </div>

            {error && <p className="voting-options-error">{error}</p>}

            {isLoading ? (
                <p>Loading...</p>
            ) : (
                <>
                    <table className="voting-options-table">
                        <thead>
                        <tr>
                            <th>Email</th>
                            <th>Created</th>
                            <th></th>
                        </tr>
                        </thead>
                        <tbody>
                        {users.map((user) => (
                            <tr key={user.id}>
                                <td>{user.email}</td>
                                <td>{new Date(user.createdAt).toLocaleDateString()}</td>
                                <td className="voting-options-actions">
                                    <button className="voting-options-edit" onClick={() => setEditingUser(user)}>
                                        Edit
                                    </button>
                                    <button
                                        className="voting-options-remove"
                                        onClick={() => setDeletingUser(user)}
                                    >
                                        Remove
                                    </button>
                                </td>
                            </tr>
                        ))}
                        </tbody>
                    </table>

                    <div className="users-pagination">
                        <button disabled={page <= 1} onClick={() => fetchUsers(page - 1)}>
                            Previous
                        </button>
                        <span>
              Page {page} of {totalPages}
            </span>
                        <button disabled={page >= totalPages} onClick={() => fetchUsers(page + 1)}>
                            Next
                        </button>
                    </div>
                </>
            )}

            {showCreate && (
                <CreateUserDialog
                    onCreate={handleCreate}
                    onCancel={() => {
                        setShowCreate(false)
                        setCreateError(null)
                    }}
                    error={createError}
                />
            )}

            {editingUser && (
                <EditUserDialog
                    initialEmail={editingUser.email}
                    onSave={handleEditSave}
                    onCancel={() => {
                        setEditingUser(null)
                        setEditError(null)
                    }}
                    error={editError}
                />
            )}

            {deletingUser && (
                <ConfirmDialog
                    title="Remove user"
                    message={`Are you sure you want to remove "${deletingUser.email}"?`}
                    confirmLabel="Remove"
                    onConfirm={handleConfirmDelete}
                    onCancel={() => setDeletingUser(null)}
                />
            )}

            {showRemoveAllConfirm && (
                <ConfirmDialog
                    title="Remove all users"
                    message="Are you sure you want to remove all users? This cannot be undone."
                    confirmLabel="Remove all"
                    onConfirm={handleConfirmRemoveAll}
                    onCancel={() => setShowRemoveAllConfirm(false)}
                />
            )}

            {revealPassword && (
                <PasswordRevealDialog
                    email={revealPassword.email}
                    password={revealPassword.password}
                    onClose={() => setRevealPassword(null)}
                />
            )}
        </div>
    )
}

export default AdminUsers