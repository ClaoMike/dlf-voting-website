import { useEffect, useRef, type ReactNode } from 'react'
import '../ConfirmDialog/ConfirmDialog.css'

type ModalProps = {
    titleId: string
    onClose: () => void
    children: ReactNode
}

/**
 * A native modal <dialog>: the browser keeps keyboard focus inside it, makes the page behind it inert,
 * closes it on Escape, and returns focus to the button that opened it.
 */
function Modal({ titleId, onClose, children }: ModalProps) {
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
            className="confirm-dialog"
            aria-labelledby={titleId}
            onCancel={(e) => {
                e.preventDefault()
                onClose()
            }}
        >
            {children}
        </dialog>
    )
}

export default Modal
