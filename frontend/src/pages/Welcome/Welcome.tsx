import { useState } from 'react'
import { useUserAuth } from '../../context/UserAuthContext'
import EditVoteDialog from '../../components/EditVoteDialog'
import VotingSection from './VotingSection'
import { useVotingData } from './useVotingData'

import './Welcome.css'

function Welcome() {
    const { email } = useUserAuth()
    const data = useVotingData()
    const [selectedId, setSelectedId] = useState('')
    const [showEditVote, setShowEditVote] = useState(false)

    const handleSubmit = async () => {
        if (!selectedId) return
        const success = await data.submitVote(selectedId)
        if (success) setSelectedId('')
    }

    const handleEditVoteSave = async (optionId: string) => {
        await data.submitVote(optionId)
        setShowEditVote(false)
    }

    return (
        <div className="welcome-page">
            <h1>
                Welcome, <span className="welcome-email">{email}</span>
            </h1>

            <section className="vote-section">
                <VotingSection
                    isLoading={data.isLoading}
                    isVotingOpen={data.isVotingOpen}
                    error={data.error}
                    myVote={data.myVote}
                    options={data.options}
                    selectedId={selectedId}
                    onSelectedIdChange={setSelectedId}
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