import { useId } from 'react'
import Modal from '../../../components/Modal/Modal'
import type { User } from './types'

import '../../../components/ConfirmDialog/ConfirmDialog.css'

type UserDetailsDialogProps = {
    user: User
    onClose: () => void
}

// Formats the backend's yyyy-MM-dd as dd-MM-yyyy without going through Date (which would shift it by time zone).
function formatDate(isoDate: string) {
    const [year, month, day] = isoDate.split('-')
    return `${day}-${month}-${year}`
}

function UserDetailsDialog({ user, onClose }: UserDetailsDialogProps) {
    const titleId = useId()
    const details: [string, string | null][] = [
        ['Username', user.username],
        ['Email', user.email],
        ['First name', user.firstName],
        ['Last name', user.lastName],
        ['Employee code', user.employeeCode],
        ['Company code', user.companyCode],
        ['Employment date', user.employmentDate && formatDate(user.employmentDate)],
        ['Electability', user.electability],
        ['Created', new Date(user.createdAt).toLocaleDateString()],
    ]

    return (
        <Modal titleId={titleId} onClose={onClose} closeOnBackdropClick>
            <h2 id={titleId} className="confirm-dialog-title">User details</h2>

            <dl className="user-details-list">
                {details
                    .filter(([, value]) => value)
                    .map(([label, value]) => (
                        <div key={label} className="user-details-item">
                            <dt>{label}</dt>
                            <dd>{value}</dd>
                        </div>
                    ))}
            </dl>

            <div className="confirm-dialog-actions">
                <button className="confirm-dialog-confirm confirm-dialog-save" onClick={onClose}>
                    Close
                </button>
            </div>
        </Modal>
    )
}

export default UserDetailsDialog
