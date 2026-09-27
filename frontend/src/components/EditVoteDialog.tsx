import { useId, useState } from 'react'
import Modal from './Modal/Modal'
import OptionList from './OptionList/OptionList'
import './ConfirmDialog/ConfirmDialog.css'

type VotingOption = {
    id: string
    name: string
}

type EditVoteDialogProps = {
    options: VotingOption[]
    initialOptionId: string
    onSave: (optionId: string) => void
    onCancel: () => void
}

function EditVoteDialog({ options, initialOptionId, onSave, onCancel }: EditVoteDialogProps) {
    const [selectedId, setSelectedId] = useState(initialOptionId)
    const titleId = useId()

    const canSubmit = selectedId !== '' && selectedId !== initialOptionId

    return (
        <Modal titleId={titleId} onClose={onCancel}>
            <h2 id={titleId} className="confirm-dialog-title">Change your vote</h2>

            <div className="edit-vote-options">
                <OptionList
                    legend="Voting options"
                    hideLegend
                    options={options}
                    selectedId={selectedId}
                    onChange={setSelectedId}
                />
            </div>

            <div className="confirm-dialog-actions">
                <button className="confirm-dialog-cancel" onClick={onCancel}>
                    Cancel
                </button>
                <button
                    className="confirm-dialog-confirm confirm-dialog-save"
                    disabled={!canSubmit}
                    onClick={() => onSave(selectedId)}
                >
                    Save vote
                </button>
            </div>
        </Modal>
    )
}

export default EditVoteDialog
