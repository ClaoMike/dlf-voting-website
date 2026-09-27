type VotingToggleSectionProps = {
    isVotingOpen: boolean
    onToggle: () => void
}

function VotingToggleSection({ isVotingOpen, onToggle }: VotingToggleSectionProps) {
    return (
        <div className="settings-toggle-row">
            <p className="settings-toggle-label" role="status">
                Voting is currently
                <span className={isVotingOpen ? 'settings-status settings-status-open' : 'settings-status settings-status-closed'}>
                    {isVotingOpen ? 'open' : 'closed'}
                </span>
            </p>
            <button
                className={
                    isVotingOpen
                        ? 'settings-toggle-button settings-toggle-close'
                        : 'settings-toggle-button settings-toggle-open'
                }
                onClick={onToggle}
            >
                {isVotingOpen ? 'Close voting' : 'Open voting'}
            </button>
        </div>
    )
}

export default VotingToggleSection
