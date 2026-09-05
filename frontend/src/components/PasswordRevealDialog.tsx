import { useState } from 'react'
import './ConfirmDialog.css'
import './PasswordRevealDialog.css'

type PasswordRevealDialogProps = {
    email: string
    password: string
    onClose: () => void
}

function PasswordRevealDialog({ email, password, onClose }: PasswordRevealDialogProps) {
    const [copied, setCopied] = useState(false)

    const handleCopy = async () => {
        await navigator.clipboard.writeText(password)
        setCopied(true)
    }

    return (
        <div className="confirm-dialog-overlay">
            <div className="confirm-dialog" role="dialog" aria-modal="true">
                <h2 className="confirm-dialog-title">Password for {email}</h2>
                <p className="password-reveal-value">{password}</p>
                <p className="password-reveal-warning">
                    This password will not be shown again once you close this window. Make sure to copy
                    and share it now.
                </p>
                <div className="confirm-dialog-actions">
                    <button className="confirm-dialog-cancel" onClick={handleCopy}>
                        {copied ? 'Copied!' : 'Copy to clipboard'}
                    </button>
                    <button className="confirm-dialog-save" onClick={onClose}>
                        Done
                    </button>
                </div>
            </div>
        </div>
    )
}

export default PasswordRevealDialog