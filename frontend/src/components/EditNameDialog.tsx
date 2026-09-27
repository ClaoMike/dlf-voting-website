import { useId, useState } from 'react'
import Modal from './Modal/Modal'
import './ConfirmDialog/ConfirmDialog.css'

type EditNameDialogProps = {
    title: string
    initialValue: string
    onSave: (newValue: string) => void
    onCancel: () => void
}

function EditNameDialog({ title, initialValue, onSave, onCancel }: EditNameDialogProps) {
    const [value, setValue] = useState(initialValue)
    const titleId = useId()
    const inputId = useId()

    const trimmed = value.trim()
    const hasChanged = trimmed !== initialValue.trim()
    const isValid = trimmed.length > 0

    return (
        <Modal titleId={titleId} onClose={onCancel}>
            <h2 id={titleId} className="confirm-dialog-title">{title}</h2>
            <div className="dialog-field">
                <label className="dialog-label" htmlFor={inputId}>Name</label>
                <input
                    id={inputId}
                    className="confirm-dialog-input"
                    type="text"
                    value={value}
                    onChange={(e) => setValue(e.target.value)}
                    data-autofocus
                />
            </div>
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
        </Modal>
    )
}

export default EditNameDialog
