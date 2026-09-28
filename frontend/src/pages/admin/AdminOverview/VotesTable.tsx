import Pagination from '../../../components/Pagination'
import type { Tab, VoteRow } from './types'

type VotesTableProps = {
    votes: VoteRow[]
    tab: Tab
    page: number
    totalPages: number
    onReset: (vote: VoteRow) => void
    onPageChange: (newPage: number) => void
}

// Admins only see whether someone voted, never what they voted for.
function VotesTable({ votes, tab, page, totalPages, onReset, onPageChange }: VotesTableProps) {
    const showVotedColumn = tab === 'all'

    if (votes.length === 0) {
        return <p>{tab === 'voted' ? 'No votes have been cast yet.' : 'No users found.'}</p>
    }

    return (
        <>
            <div className="table-scroll">
                <table className="voting-options-table">
                    <caption className="visually-hidden">Votes</caption>
                    <thead>
                    <tr>
                        <th scope="col">Name</th>
                        <th scope="col">Username</th>
                        {showVotedColumn && <th scope="col">Voted</th>}
                        <th scope="col"><span className="visually-hidden">Actions</span></th>
                    </tr>
                    </thead>
                    <tbody>
                    {votes.map((vote) => (
                        <tr key={vote.userId}>
                            <td>{[vote.firstName, vote.lastName].filter(Boolean).join(' ')}</td>
                            <td>{vote.username}</td>
                            {showVotedColumn && <td>{vote.hasVoted ? 'True' : 'False'}</td>}
                            <td className="voting-options-actions">
                                {vote.hasVoted && (
                                    <button className="voting-options-remove" onClick={() => onReset(vote)}>
                                        Reset<span className="visually-hidden"> vote of {vote.username}</span>
                                    </button>
                                )}
                            </td>
                        </tr>
                    ))}
                    </tbody>
                </table>
            </div>

            <Pagination page={page} totalPages={totalPages} onPageChange={onPageChange} />
        </>
    )
}

export default VotesTable
