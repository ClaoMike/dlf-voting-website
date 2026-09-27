import { useId, useState } from 'react'
import Modal from '../Modal/Modal'

import '../ConfirmDialog/ConfirmDialog.css'
import './PasswordRevealDialog.css'

type PasswordRevealDialogProps = {
    accountName: string
    password: string
    onClose: () => void
}

function PasswordRevealDialog({ accountName, password, onClose }: PasswordRevealDialogProps) {
    const [copied, setCopied] = useState(false)
    const titleId = useId()

    const handleCopy = async () => {
        await navigator.clipboard.writeText(password)
        setCopied(true)
    }

    return (
        <Modal titleId={titleId} onClose={onClose}>
            <h2 id={titleId} className="confirm-dialog-title">Password for {accountName}</h2>
            <p className="password-reveal-value">{password}</p>
            <p className="password-reveal-warning">
                This password will not be shown again once you close this window. Make sure to copy
                and share it now.
            </p>
            <div className="confirm-dialog-actions">
                <button className="confirm-dialog-cancel" onClick={handleCopy}>
                    {copied ? 'Copied!' : 'Copy to clipboard'}
                </button>
                <button className="confirm-dialog-confirm confirm-dialog-save" onClick={onClose}>
                    Done
                </button>
            </div>
            <p className="visually-hidden" aria-live="polite">{copied ? 'Password copied to the clipboard.' : ''}</p>
        </Modal>
    )
}

export default PasswordRevealDialog
