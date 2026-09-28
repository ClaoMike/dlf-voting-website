import { useState } from 'react'
import { useUserAuth } from '../../context/useUserAuth'
import EditVoteDialog from '../../components/EditVoteDialog'
import VotingSection from './VotingSection'
import { useVotingData } from './useVotingData'

import './Welcome.css'

function Welcome() {
    const { username, fullName } = useUserAuth()
    const data = useVotingData()
    const [selectedId, setSelectedId] = useState('')
    const [showEditVote, setShowEditVote] = useState(false)
    const [justVoted, setJustVoted] = useState(false)
    const [selectionMissing, setSelectionMissing] = useState(false)

    const handleSubmit = async () => {
        if (data.isSubmitting) return
        if (!selectedId) {
            setSelectionMissing(true)
            return
        }
        setSelectionMissing(false)
        const success = await data.submitVote(selectedId)
        if (success) {
            setSelectedId('')
            setJustVoted(true)
        }
    }

    const handleEditVoteSave = async (optionId: string) => {
        await data.submitVote(optionId)
        setShowEditVote(false)
    }

    return (
        <div className="welcome-page">
            <header className="welcome-header">
                <h1>
                    Welcome, <span className="welcome-name">{fullName ?? username}</span>
                </h1>
            </header>

            <section className="vote-section" aria-label="Ballot">
                <VotingSection
                    isLoading={data.isLoading}
                    isVotingOpen={data.isVotingOpen}
                    loadError={data.loadError}
                    submitProblem={
                        selectionMissing ? { message: 'Choose an option before submitting.' } : data.submitProblem
                    }
                    isSubmitting={data.isSubmitting}
                    myVote={data.myVote}
                    options={data.options}
                    selectedId={selectedId}
                    focusConfirmation={justVoted}
                    onSelectedIdChange={(id) => {
                        setSelectedId(id)
                        setSelectionMissing(false)
                    }}
                    onSubmit={handleSubmit}
                    onEditVoteClick={() => setShowEditVote(true)}
                />
            </section>

            {showEditVote && data.myVote?.votingOptionId && (
                <EditVoteDialog
                    options={data.options}
                    initialOptionId={data.myVote.votingOptionId}
                    onSave={handleEditVoteSave}
                    onCancel={() => setShowEditVote(false)}
                />
            )}
        </div>
    )
}

export default Welcome
