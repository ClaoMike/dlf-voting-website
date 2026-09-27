import { useEffect, useRef, type SubmitEvent } from 'react'
import { Link } from 'react-router-dom'
import OptionList from '../../components/OptionList/OptionList'
import SproutIcon from '../../components/Decor/SproutIcon'
import type { SubmitProblem } from './useVotingData'
import type { MyVote, VotingOption } from './types'

const CHANGE_HINT = 'You can change your vote until voting closes.'

const dateFormat = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' })

type VotingSectionProps = {
    isLoading: boolean
    isVotingOpen: boolean | null
    loadError: string | null
    submitProblem: SubmitProblem | null
    isSubmitting: boolean
    myVote: MyVote | null
    options: VotingOption[]
    selectedId: string
    // True right after this visit's vote was saved: the confirmation takes focus so screen readers announce it.
    focusConfirmation: boolean
    onSelectedIdChange: (id: string) => void
    onSubmit: () => void
    onEditVoteClick: () => void
}

function SubmitProblemMessage({ problem }: { problem: SubmitProblem | null }) {
    return (
        <p className="vote-problem" role="alert">
            {problem?.message}
            {problem?.sessionExpired && (
                <>
                    {' '}
                    <Link to="/login">Sign in again</Link>
                </>
            )}
        </p>
    )
}

function VotingSection({
                           isLoading,
                           isVotingOpen,
                           loadError,
                           submitProblem,
                           isSubmitting,
                           myVote,
                           options,
                           selectedId,
                           focusConfirmation,
                           onSelectedIdChange,
                           onSubmit,
                           onEditVoteClick,
                       }: VotingSectionProps) {
    const confirmationRef = useRef<HTMLHeadingElement>(null)

    useEffect(() => {
        if (focusConfirmation && myVote?.hasVoted) confirmationRef.current?.focus()
    }, [focusConfirmation, myVote?.hasVoted])

    if (isLoading) return <p role="status" className="vote-card">Loading the ballot…</p>
    if (loadError) return <p role="alert" className="vote-card vote-problem">{loadError}</p>

    if (isVotingOpen === false) {
        return (
            <div className="vote-card vote-card-centered">
                <SproutIcon />
                <h2>Voting is closed</h2>
                <p>Voting is not open at the moment.</p>
            </div>
        )
    }

    if (myVote?.hasVoted) {
        return (
            <div className="vote-card vote-card-centered">
                <SproutIcon />
                <h2 ref={confirmationRef} tabIndex={-1}>Your vote is registered</h2>
                <p aria-live="polite">
                    You voted for <strong className="vote-choice">{myVote.votingOptionName}</strong>
                </p>
                {myVote.updatedAt && (
                    <p className="vote-meta">Saved {dateFormat.format(new Date(myVote.updatedAt))}</p>
                )}
                <SubmitProblemMessage problem={submitProblem} />
                <button className="vote-button vote-button-secondary" onClick={onEditVoteClick}>
                    Change vote
                </button>
                <p className="vote-meta">{CHANGE_HINT}</p>
            </div>
        )
    }

    if (options.length === 0) {
        return (
            <div className="vote-card vote-card-centered">
                <SproutIcon />
                <h2>Nothing to vote on yet</h2>
                <p>The options have not been published. Please check back later.</p>
            </div>
        )
    }

    const handleSubmit = (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault()
        onSubmit()
    }

    return (
        <form className="vote-card" onSubmit={handleSubmit}>
            <OptionList
                legend="Cast your vote"
                hint={`Choose one option. ${CHANGE_HINT}`}
                options={options}
                selectedId={selectedId}
                onChange={onSelectedIdChange}
            />
            <SubmitProblemMessage problem={submitProblem} />
            <button type="submit" className="vote-button" aria-disabled={isSubmitting}>
                {isSubmitting ? 'Submitting…' : 'Submit vote'}
            </button>
        </form>
    )
}

export default VotingSection
