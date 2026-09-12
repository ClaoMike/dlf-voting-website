import type { Tab, VoteRow } from './types'

type VotesTableProps = {
    votes: VoteRow[]
    tab: Tab
    page: number
    totalPages: number
    onEdit: (vote: VoteRow) => void
    onDelete: (vote: VoteRow) => void
    onPageChange: (newPage: number) => void
}

function VotesTable({ votes, tab, page, totalPages, onEdit, onDelete, onPageChange }: VotesTableProps) {
    if (votes.length === 0) {
        return <p>{tab === 'voted' ? 'No votes have been cast yet.' : 'No users found.'}</p>
    }

    return (
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
                        <td>{vote.votingOptionName ?? ''}</td>
                        <td className="voting-options-actions">
                            <button className="voting-options-edit" onClick={() => onEdit(vote)}>
                                Edit
                            </button>
                            {vote.votingOptionId && (
                                <button className="voting-options-remove" onClick={() => onDelete(vote)}>
                                    Remove
                                </button>
                            )}
                        </td>
                    </tr>
                ))}
                </tbody>
            </table>

            <div className="users-pagination">
                <button disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
                    Previous
                </button>
                <span>
          Page {page} of {totalPages}
        </span>
                <button disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
                    Next
                </button>
            </div>
        </>
    )
}

export default VotesTable