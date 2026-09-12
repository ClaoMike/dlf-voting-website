import type { MyVote, VotingOption } from './types'

type VotingSectionProps = {
    isLoading: boolean
    isVotingOpen: boolean | null
    error: string | null
    myVote: MyVote | null
    options: VotingOption[]
    selectedId: string
    onSelectedIdChange: (id: string) => void
    onSubmit: () => void
    onEditVoteClick: () => void
}

function VotingSection({
                           isLoading,
                           isVotingOpen,
                           error,
                           myVote,
                           options,
                           selectedId,
                           onSelectedIdChange,
                           onSubmit,
                           onEditVoteClick,
                       }: VotingSectionProps) {
    if (isLoading) return <p>Loading voting options...</p>
    if (isVotingOpen === false) return <p className="voting-closed-message">Voting polls are closed.</p>
    if (error) return <p className="voting-options-error">{error}</p>

    if (myVote?.hasVoted) {
        return (
            <>
                <p className="current-vote-text">
                    You voted for: <strong>{myVote.votingOptionName}</strong>
                </p>
                <button className="vote-submit" onClick={onEditVoteClick}>
                    Edit Vote
                </button>
            </>
        )
    }

    return (
        <>
            <select className="vote-select" value={selectedId} onChange={(e) => onSelectedIdChange(e.target.value)}>
                <option value=""></option>
                {options.map((option) => (
                    <option key={option.id} value={option.id}>
                        {option.name}
                    </option>
                ))}
            </select>

            <button className="vote-submit" disabled={selectedId === ''} onClick={onSubmit}>
                Submit vote
            </button>
        </>
    )
}

export default VotingSection