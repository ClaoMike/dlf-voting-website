import { useId } from 'react'
import Modal from '../Modal/Modal'
import './ConfirmDialog.css'

type ConfirmDialogProps = {
    title: string
    message: string
    confirmLabel: string
    cancelLabel?: string
    onConfirm: () => void
    onCancel: () => void
}

function ConfirmDialog({
                           title,
                           message,
                           confirmLabel,
                           cancelLabel = 'Cancel',
                           onConfirm,
                           onCancel,
                       }: ConfirmDialogProps) {
    const titleId = useId()

    return (
        <Modal titleId={titleId} onClose={onCancel}>
            <h2 id={titleId} className="confirm-dialog-title">{title}</h2>
            <p className="confirm-dialog-message">{message}</p>
            <div className="confirm-dialog-actions">
                <button className="confirm-dialog-cancel" onClick={onCancel}>
                    {cancelLabel}
                </button>
                <button className="confirm-dialog-confirm" onClick={onConfirm}>
                    {confirmLabel}
                </button>
            </div>
        </Modal>
    )
}

export default ConfirmDialog
