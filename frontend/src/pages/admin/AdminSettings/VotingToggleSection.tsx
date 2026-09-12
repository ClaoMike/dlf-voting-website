type VotingToggleSectionProps = {
    isVotingOpen: boolean
    onToggle: () => void
}

function VotingToggleSection({ isVotingOpen, onToggle }: VotingToggleSectionProps) {
    return (
        <div className="settings-toggle-row">
      <span className="settings-toggle-label">
        Voting is currently {isVotingOpen ? 'open' : 'closed'}
      </span>
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