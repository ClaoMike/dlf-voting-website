import { useState } from 'react'
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

    const canSubmit = selectedId !== '' && selectedId !== initialOptionId

    return (
        <div className="confirm-dialog-overlay">
            <div className="confirm-dialog" role="dialog" aria-modal="true">
                <h2 className="confirm-dialog-title">Edit your vote</h2>

                <select
                    className="confirm-dialog-input"
                    value={selectedId}
                    onChange={(e) => setSelectedId(e.target.value)}
                >
                    <option value=""></option>
                    {options.map((option) => (
                        <option key={option.id} value={option.id}>
                            {option.name}
                        </option>
                    ))}
                </select>

                <div className="confirm-dialog-actions">
                    <button className="confirm-dialog-cancel" onClick={onCancel}>
                        Cancel
                    </button>
                    <button
                        className="confirm-dialog-confirm confirm-dialog-save"
                        disabled={!canSubmit}
                        onClick={() => onSave(selectedId)}
                    >
                        Submit new vote
                    </button>
                </div>
            </div>
        </div>
    )
}

export default EditVoteDialog