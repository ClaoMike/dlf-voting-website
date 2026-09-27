import Pagination from '../../../components/Pagination'
import type { Administrator } from './types'

type AdministratorsTableProps = {
    admins: Administrator[]
    currentAdminUsername: string | null
    page: number
    totalPages: number
    onEdit: (admin: Administrator) => void
    onDelete: (admin: Administrator) => void
    onPageChange: (newPage: number) => void
}

function AdministratorsTable({
                                 admins,
                                 currentAdminUsername,
                                 page,
                                 totalPages,
                                 onEdit,
                                 onDelete,
                                 onPageChange,
                             }: AdministratorsTableProps) {
    return (
        <>
            <div className="table-scroll">
                <table className="voting-options-table">
                    <caption className="visually-hidden">Administrators</caption>
                    <thead>
                    <tr>
                        <th scope="col">Username</th>
                        <th scope="col">Created</th>
                        <th scope="col"><span className="visually-hidden">Actions</span></th>
                    </tr>
                    </thead>
                    <tbody>
                    {admins.map((admin) => {
                        const isSelf = admin.username === currentAdminUsername
                        return (
                            <tr key={admin.id}>
                                <td>{admin.username}</td>
                                <td>{new Date(admin.createdAt).toLocaleDateString()}</td>
                                <td className="voting-options-actions">
                                    {isSelf ? (
                                        <span className="administrators-self-label">This is you</span>
                                    ) : (
                                        <>
                                            <button className="voting-options-edit" onClick={() => onEdit(admin)}>
                                                Edit<span className="visually-hidden"> {admin.username}</span>
                                            </button>
                                            <button className="voting-options-remove" onClick={() => onDelete(admin)}>
                                                Remove<span className="visually-hidden"> {admin.username}</span>
                                            </button>
                                        </>
                                    )}
                                </td>
                            </tr>
                        )
                    })}
                    </tbody>
                </table>
            </div>

            <Pagination page={page} totalPages={totalPages} onPageChange={onPageChange} />
        </>
    )
}

export default AdministratorsTable
