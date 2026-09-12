import type { Administrator } from './types'

type AdministratorsTableProps = {
    admins: Administrator[]
    currentAdminEmail: string | null
    page: number
    totalPages: number
    onEdit: (admin: Administrator) => void
    onDelete: (admin: Administrator) => void
    onPageChange: (newPage: number) => void
}

function AdministratorsTable({
                                 admins,
                                 currentAdminEmail,
                                 page,
                                 totalPages,
                                 onEdit,
                                 onDelete,
                                 onPageChange,
                             }: AdministratorsTableProps) {
    return (
        <>
            <table className="voting-options-table">
                <thead>
                <tr>
                    <th>Email</th>
                    <th>Created</th>
                    <th></th>
                </tr>
                </thead>
                <tbody>
                {admins.map((admin) => {
                    const isSelf = admin.email === currentAdminEmail
                    return (
                        <tr key={admin.id}>
                            <td>{admin.email}</td>
                            <td>{new Date(admin.createdAt).toLocaleDateString()}</td>
                            <td className="voting-options-actions">
                                {isSelf ? (
                                    <span className="administrators-self-label">This is you</span>
                                ) : (
                                    <>
                                        <button className="voting-options-edit" onClick={() => onEdit(admin)}>
                                            Edit
                                        </button>
                                        <button className="voting-options-remove" onClick={() => onDelete(admin)}>
                                            Remove
                                        </button>
                                    </>
                                )}
                            </td>
                        </tr>
                    )
                })}
                </tbody>
            </table>

            <div className="users-pagination">
                <button disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
                    Previous
                </button>
                <span>
          Page {page} of {totalPages}
        </span>
                <button disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
                    Next
                </button>
            </div>
        </>
    )
}

export default AdministratorsTable