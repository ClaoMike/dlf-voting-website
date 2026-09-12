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
    return (
        <div className="confirm-dialog-overlay">
            <div className="confirm-dialog" role="dialog" aria-modal="true">
                <h2 className="confirm-dialog-title">{title}</h2>
                <p className="confirm-dialog-message">{message}</p>
                <div className="confirm-dialog-actions">
                    <button className="confirm-dialog-cancel" onClick={onCancel}>
                        {cancelLabel}
                    </button>
                    <button className="confirm-dialog-confirm" onClick={onConfirm}>
                        {confirmLabel}
                    </button>
                </div>
            </div>
        </div>
    )
}

export default ConfirmDialog