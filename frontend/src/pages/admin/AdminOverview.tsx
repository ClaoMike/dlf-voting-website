import { useEffect, useState } from 'react'
import ConfirmDialog from '../../components/ConfirmDialog'
import EditVoteDialog from '../../components/EditVoteDialog'
import '../admin/AdminVotingOptions.css'
import './AdminOverview.css'

type VoteRow = {
    userId: string
    email: string
    votingOptionId: string
    votingOptionName: string
    updatedAt: string
}

type PagedVotes = {
    items: VoteRow[]
    totalCount: number
    page: number
    pageSize: number
}

type VotingOption = {
    id: string
    name: string
}

const VOTES_API = 'http://localhost:5120/api/votes'
const OPTIONS_API = 'http://localhost:5120/api/voting-options'

function AdminOverview() {
    const [votes, setVotes] = useState<VoteRow[]>([])
    const [options, setOptions] = useState<VotingOption[]>([])
    const [page, setPage] = useState(1)
    const [totalCount, setTotalCount] = useState(0)
    const [pageSize, setPageSize] = useState(25)
    const [isLoading, setIsLoading] = useState(true)
    const [error, setError] = useState<string | null>(null)

    const [editingVote, setEditingVote] = useState<VoteRow | null>(null)
    const [deletingVote, setDeletingVote] = useState<VoteRow | null>(null)

    const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1)

    const fetchVotes = async (targetPage: number) => {
        setIsLoading(true)
        setError(null)
        try {
            const res = await fetch(`${VOTES_API}?page=${targetPage}`, { credentials: 'include' })
            if (!res.ok) throw new Error('Failed to load votes.')
            const data: PagedVotes = await res.json()
            setVotes(data.items)
            setTotalCount(data.totalCount)
            setPageSize(data.pageSize)
            setPage(data.page)
        } catch {
            setError('Could not load votes.')
        } finally {
            setIsLoading(false)
        }
    }

    const fetchOptions = async () => {
        try {
            const res = await fetch(OPTIONS_API, { credentials: 'include' })
            if (res.ok) setOptions(await res.json())
        } catch {
            // options are only needed for the edit dialog; a failure here surfaces when the dialog opens empty
        }
    }

    useEffect(() => {
        fetchVotes(1)
        fetchOptions()
    }, [])

    const handleEditSave = async (optionId: string) => {
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
            await fetchVotes(page)
        } catch {
            setError('Failed to update vote.')
            setEditingVote(null)
        }
    }

    const handleConfirmDelete = async () => {
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
            await fetchVotes(page)
        } catch {
            setError('Failed to remove vote.')
            setDeletingVote(null)
        }
    }

    return (
        <div className="voting-options-page">
            <h1>Overview</h1>

            {error && <p className="voting-options-error">{error}</p>}

            {isLoading ? (
                <p>Loading...</p>
            ) : votes.length === 0 ? (
                <p>No votes have been cast yet.</p>
            ) : (
                <>
                    <table className="voting-options-table">
                        <thead>
                        <tr>
                            <th>Email</th>
                            <th>Vote</th>
                            <th></th>
                        </tr>
                        </thead>
                        <tbody>
                        {votes.map((vote) => (
                            <tr key={vote.userId}>
                                <td>{vote.email}</td>
                                <td>{vote.votingOptionName}</td>
                                <td className="voting-options-actions">
                                    <button className="voting-options-edit" onClick={() => setEditingVote(vote)}>
                                        Edit
                                    </button>
                                    <button
                                        className="voting-options-remove"
                                        onClick={() => setDeletingVote(vote)}
                                    >
                                        Remove
                                    </button>
                                </td>
                            </tr>
                        ))}
                        </tbody>
                    </table>

                    <div className="users-pagination">
                        <button disabled={page <= 1} onClick={() => fetchVotes(page - 1)}>
                            Previous
                        </button>
                        <span>
              Page {page} of {totalPages}
            </span>
                        <button disabled={page >= totalPages} onClick={() => fetchVotes(page + 1)}>
                            Next
                        </button>
                    </div>
                </>
            )}

            {editingVote && (
                <EditVoteDialog
                    options={options}
                    initialOptionId={editingVote.votingOptionId}
                    onSave={handleEditSave}
                    onCancel={() => setEditingVote(null)}
                />
            )}

            {deletingVote && (
                <ConfirmDialog
                    title="Remove vote"
                    message={`Are you sure you want to remove ${deletingVote.email}'s vote?`}
                    confirmLabel="Remove"
                    onConfirm={handleConfirmDelete}
                    onCancel={() => setDeletingVote(null)}
                />
            )}
        </div>
    )
}

export default AdminOverview