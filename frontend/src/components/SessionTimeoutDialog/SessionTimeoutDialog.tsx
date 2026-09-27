import { useId } from 'react'
import Modal from '../Modal/Modal'
import './SessionTimeoutDialog.css'

type SessionTimeoutDialogProps = {
    secondsLeft: number
    onStaySignedIn: () => void
    onSignOut: () => void
}

function formatCountdown(seconds: number) {
    const minutes = Math.floor(seconds / 60)
    return `${minutes}:${String(seconds % 60).padStart(2, '0')}`
}

/** Shown shortly before an inactive session ends. Escape counts as "Stay signed in". */
function SessionTimeoutDialog({ secondsLeft, onStaySignedIn, onSignOut }: SessionTimeoutDialogProps) {
    const titleId = useId()
    const messageId = useId()

    return (
        <Modal titleId={titleId} onClose={onStaySignedIn} className="session-warning">
            <h2 id={titleId} className="confirm-dialog-title">Are you still there?</h2>
            <p id={messageId} className="confirm-dialog-message">
                For your security, you will be signed out soon because there has been no activity.
            </p>
            {/* Visual countdown; screen readers get the sentence above once, not a number every second. */}
            <p className="session-warning-countdown" aria-hidden="true">
                {formatCountdown(secondsLeft)}
            </p>
            <div className="confirm-dialog-actions">
                <button className="confirm-dialog-cancel" onClick={onSignOut}>
                    Sign out
                </button>
                <button className="confirm-dialog-confirm confirm-dialog-save" onClick={onStaySignedIn} data-autofocus>
                    Stay signed in
                </button>
            </div>
        </Modal>
    )
}

export default SessionTimeoutDialog
