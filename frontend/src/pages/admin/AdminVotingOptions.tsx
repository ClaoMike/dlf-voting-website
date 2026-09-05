import { useEffect, useState } from 'react'
import ConfirmDialog from '../../components/ConfirmDialog'
import EditNameDialog from '../../components/EditNameDialog'
import './AdminVotingOptions.css'

type VotingOption = {
    id: string
    name: string
    createdAt: string
}

const API_BASE = 'http://localhost:5120/api/voting-options'

function AdminVotingOptions() {
    const [options, setOptions] = useState<VotingOption[]>([])
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const [newName, setNewName] = useState('')
    const [isAdding, setIsAdding] = useState(false)

    const [editingOption, setEditingOption] = useState<VotingOption | null>(null)
    const [deletingOption, setDeletingOption] = useState<VotingOption | null>(null)

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

    const handleAdd = async () => {
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
            await fetchOptions()
        } finally {
            setIsAdding(false)
        }
    }

    const handleSaveEdit = async (newValue: string) => {
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
            await fetchOptions()
        } catch {
            setError('Failed to update voting option.')
            setEditingOption(null)
        }
    }

    const handleConfirmDelete = async () => {
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
            await fetchOptions()
        } catch {
            setError('Failed to delete voting option.')
            setDeletingOption(null)
        }
    }

    return (
        <div className="voting-options-page">
            <h1>Voting Options</h1>

            <div className="voting-options-add-row">
                <input
                    type="text"
                    placeholder="New voting option name"
                    value={newName}
                    onChange={(e) => setNewName(e.target.value)}
                />
                <button
                    disabled={!newName.trim() || isAdding}
                    onClick={handleAdd}
                >
                    Add option
                </button>
            </div>

            {error && <p className="voting-options-error">{error}</p>}

            {isLoading ? (
                <p>Loading...</p>
            ) : (
                <table className="voting-options-table">
                    <thead>
                    <tr>
                        <th>Name</th>
                        <th>Created</th>
                        <th></th>
                    </tr>
                    </thead>
                    <tbody>
                    {options.map((option) => (
                        <tr key={option.id}>
                            <td>{option.name}</td>
                            <td>{new Date(option.createdAt).toLocaleDateString()}</td>
                            <td className="voting-options-actions">

                                <button
                                    className="voting-options-edit"
                                    onClick={() => setEditingOption(option)}
                                >
                                    Edit
                                </button>
                                
                                <button
                                    className="voting-options-remove"
                                    onClick={() => setDeletingOption(option)}
                                >
                                    Remove
                                </button>
                            </td>
                        </tr>
                    ))}
                    </tbody>
                </table>
            )}

            {editingOption && (
                <EditNameDialog
                    title="Edit voting option"
                    initialValue={editingOption.name}
                    onSave={handleSaveEdit}
                    onCancel={() => setEditingOption(null)}
                />
            )}

            {deletingOption && (
                <ConfirmDialog
                    title="Remove voting option"
                    message={`Are you sure you want to remove "${deletingOption.name}"?`}
                    confirmLabel="Remove"
                    onConfirm={handleConfirmDelete}
                    onCancel={() => setDeletingOption(null)}
                />
            )}
        </div>
    )
}

export default AdminVotingOptions