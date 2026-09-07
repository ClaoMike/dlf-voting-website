import { useEffect, useState } from 'react'
import ConfirmDialog from '../../components/ConfirmDialog'
import CreateUserDialog from '../../components/CreateUserDialog'
import EditUserDialog from '../../components/EditUserDialog'
import PasswordRevealDialog from '../../components/PasswordRevealDialog'
import { useAdminAuth } from '../../context/AdminAuthContext'
import '../admin/AdminVotingOptions.css'
import '../admin/AdminUsers.css'

type Administrator = {
    id: string
    email: string
    createdAt: string
}

type PagedAdministrators = {
    items: Administrator[]
    totalCount: number
    page: number
    pageSize: number
}

const API_BASE = 'http://localhost:5120/api/administrators'

function AdminAdministrators() {
    const { email: currentAdminEmail } = useAdminAuth()

    const [admins, setAdmins] = useState<Administrator[]>([])
    const [page, setPage] = useState(1)
    const [totalCount, setTotalCount] = useState(0)
    const [pageSize, setPageSize] = useState(25)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const [showCreate, setShowCreate] = useState(false)
    const [createError, setCreateError] = useState<string | null>(null)

    const [editingAdmin, setEditingAdmin] = useState<Administrator | null>(null)
    const [editError, setEditError] = useState<string | null>(null)

    const [deletingAdmin, setDeletingAdmin] = useState<Administrator | null>(null)

    const [revealPassword, setRevealPassword] = useState<{ email: string; password: string } | null>(null)

    const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1)

    const fetchAdmins = async (targetPage: number) => {
        setIsLoading(true)
        setError(null)
        try {
            const res = await fetch(`${API_BASE}?page=${targetPage}`, { credentials: 'include' })
            if (!res.ok) throw new Error('Failed to load administrators.')
            const data: PagedAdministrators = await res.json()
            setAdmins(data.items)
            setTotalCount(data.totalCount)
            setPageSize(data.pageSize)
            setPage(data.page)
        } catch {
            setError('Could not load administrators.')
        } finally {
            setIsLoading(false)
        }
    }

    useEffect(() => {
        fetchAdmins(1)
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
                setCreateError(body?.message ?? 'Failed to create administrator.')
                return
            }

            setShowCreate(false)
            setRevealPassword({ email, password })
            await fetchAdmins(1)
        } catch {
            setCreateError('Failed to create administrator.')
        }
    }

    const handleEditSave = async (newEmail: string | null, newPassword: string | null) => {
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
            await fetchAdmins(page)

            if (newPassword) {
                setRevealPassword({ email: finalEmail, password: newPassword })
            }
        } catch {
            setEditError('Failed to update administrator.')
        }
    }

    const handleConfirmDelete = async () => {
        if (!deletingAdmin) return

        setError(null)
        try {
            const res = await fetch(`${API_BASE}/${deletingAdmin.id}`, {
                method: 'DELETE',
                credentials: 'include',
            })

            if (!res.ok && res.status !== 404) {
                const body = await res.json().catch(() => null)
                setError(body?.message ?? 'Failed to remove administrator.')
            }

            setDeletingAdmin(null)
            await fetchAdmins(page)
        } catch {
            setError('Failed to remove administrator.')
            setDeletingAdmin(null)
        }
    }

    return (
        <div className="voting-options-page">
            <h1>Administrators</h1>

            <button className="voting-options-add-row-button" onClick={() => setShowCreate(true)}>
                Add administrator
            </button>

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
                        {admins.map((admin) => {
                            const isSelf = admin.email === currentAdminEmail
                            return (
                                <tr key={admin.id}>
                                    <td>{admin.email}</td>
                                    <td>{new Date(admin.createdAt).toLocaleDateString()}</td>
                                    <td className="voting-options-actions">
                                        {isSelf ? (
                                            <span className="administrators-self-label">This is you</span>
                                        ) : (
                                            <>
                                                <button
                                                    className="voting-options-edit"
                                                    onClick={() => setEditingAdmin(admin)}
                                                >
                                                    Edit
                                                </button>
                                                <button
                                                    className="voting-options-remove"
                                                    onClick={() => setDeletingAdmin(admin)}
                                                >
                                                    Remove
                                                </button>
                                            </>
                                        )}
                                    </td>
                                </tr>
                            )
                        })}
                        </tbody>
                    </table>

                    <div className="users-pagination">
                        <button disabled={page <= 1} onClick={() => fetchAdmins(page - 1)}>
                            Previous
                        </button>
                        <span>
              Page {page} of {totalPages}
            </span>
                        <button disabled={page >= totalPages} onClick={() => fetchAdmins(page + 1)}>
                            Next
                        </button>
                    </div>
                </>
            )}

            {showCreate && (
                <CreateUserDialog
                    title="New administrator"
                    submitLabel="Create administrator"
                    onCreate={handleCreate}
                    onCancel={() => {
                        setShowCreate(false)
                        setCreateError(null)
                    }}
                    error={createError}
                />
            )}

            {editingAdmin && (
                <EditUserDialog
                    title="Edit administrator"
                    initialEmail={editingAdmin.email}
                    onSave={handleEditSave}
                    onCancel={() => {
                        setEditingAdmin(null)
                        setEditError(null)
                    }}
                    error={editError}
                />
            )}

            {deletingAdmin && (
                <ConfirmDialog
                    title="Remove administrator"
                    message={`Are you sure you want to remove "${deletingAdmin.email}"?`}
                    confirmLabel="Remove"
                    onConfirm={handleConfirmDelete}
                    onCancel={() => setDeletingAdmin(null)}
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

export default AdminAdministrators