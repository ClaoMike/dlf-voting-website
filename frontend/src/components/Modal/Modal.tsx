import { useEffect, useRef, type ReactNode } from 'react'
import '../ConfirmDialog/ConfirmDialog.css'

type ModalProps = {
    titleId: string
    onClose: () => void
    children: ReactNode
    // Also close when the dimmed area around the dialog is clicked.
    closeOnBackdropClick?: boolean
    className?: string
}

/**
 * A native modal <dialog>: the browser keeps keyboard focus inside it, makes the page behind it inert,
 * closes it on Escape, and focus goes back to the button that opened it.
 */
function Modal({ titleId, onClose, children, closeOnBackdropClick = false, className }: ModalProps) {
    const ref = useRef<HTMLDialogElement>(null)

    useEffect(() => {
        const dialog = ref.current
        const opener = document.activeElement instanceof HTMLElement ? document.activeElement : null
        dialog?.showModal()
        return () => {
            dialog?.close()
            // React has already removed the dialog by now, so the browser can't restore focus itself.
            opener?.focus()
        }
    }, [])

    return (
        <dialog
            ref={ref}
            className={className ? `confirm-dialog ${className}` : 'confirm-dialog'}
            aria-labelledby={titleId}
            onCancel={(e) => {
                e.preventDefault()
                onClose()
            }}
            // The body fills the dialog, so a click that lands on the <dialog> itself is on the backdrop.
            onClick={(e) => {
                if (closeOnBackdropClick && e.target === e.currentTarget) onClose()
            }}
        >
            <div className="modal-body">{children}</div>
        </dialog>
    )
}

export default Modal
