import { useId } from 'react'
import Modal from '../../../components/Modal/Modal'
import type { ImportMode } from './types'

import '../../../components/ConfirmDialog/ConfirmDialog.css'

type ImportUsersDialogProps = {
    onChoose: (mode: ImportMode) => void
    onCancel: () => void
}

function ImportUsersDialog({ onChoose, onCancel }: ImportUsersDialogProps) {
    const titleId = useId()

    return (
        <Modal titleId={titleId} onClose={onCancel} className="import-users-dialog">
            <h2 id={titleId} className="confirm-dialog-title">Import users from Excel</h2>

            <button className="import-users-option" onClick={() => onChoose('emails')}>
                <strong>Import email addresses</strong>
                <span>
                    An Excel file (.xlsx) with email addresses in the first column. You get back an Excel
                    file with each email and its generated username and password.
                </span>
            </button>

            <button className="import-users-option" onClick={() => onChoose('employees')}>
                <strong>Import employees</strong>
                <span>
                    The employee Excel file (.xlsx) with the columns Kode, Fornavn, Efternavn,
                    Virksomhedskode, Ansættelsesdato and Valgbarhed. You get back the same file with a
                    generated Username and Password added to each row.
                </span>
            </button>

            <div className="confirm-dialog-actions">
                <button className="confirm-dialog-cancel" onClick={onCancel}>
                    Cancel
                </button>
            </div>
        </Modal>
    )
}

export default ImportUsersDialog
