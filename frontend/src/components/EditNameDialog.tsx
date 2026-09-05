import { useState } from 'react'
import './ConfirmDialog.css'

type EditNameDialogProps = {
    title: string
    initialValue: string
    onSave: (newValue: string) => void
    onCancel: () => void
}

function EditNameDialog({ title, initialValue, onSave, onCancel }: EditNameDialogProps) {
    const [value, setValue] = useState(initialValue)

    const trimmed = value.trim()
    const hasChanged = trimmed !== initialValue.trim()
    const isValid = trimmed.length > 0

    return (
        <div className="confirm-dialog-overlay">
            <div className="confirm-dialog" role="dialog" aria-modal="true">
                <h2 className="confirm-dialog-title">{title}</h2>
                <input
                    className="confirm-dialog-input"
                    type="text"
                    value={value}
                    onChange={(e) => setValue(e.target.value)}
                    autoFocus
                />
                <div className="confirm-dialog-actions">
                    <button className="confirm-dialog-cancel" onClick={onCancel}>
                        Cancel
                    </button>
                    <button
                        className="confirm-dialog-confirm confirm-dialog-save"
                        disabled={!isValid || !hasChanged}
                        onClick={() => onSave(trimmed)}
                    >
                        Save
                    </button>
                </div>
            </div>
        </div>
    )
}

export default EditNameDialog