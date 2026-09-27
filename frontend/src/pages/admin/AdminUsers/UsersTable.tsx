import Pagination from '../../../components/Pagination'
import type { UserListItem } from './types'

type UsersTableProps = {
    users: UserListItem[]
    page: number
    totalPages: number
    onSelect: (user: UserListItem) => void
    onEdit: (user: UserListItem) => void
    onDelete: (user: UserListItem) => void
    onPageChange: (newPage: number) => void
}

function UsersTable({ users, page, totalPages, onSelect, onEdit, onDelete, onPageChange }: UsersTableProps) {
    if (users.length === 0) {
        return <p>No users yet. Add one, or import them from Excel.</p>
    }

    return (
        <>
            <div className="table-scroll">
                <table className="voting-options-table">
                    <caption className="visually-hidden">Users</caption>
                    <thead>
                    <tr>
                        <th scope="col">Name</th>
                        <th scope="col">Username</th>
                        <th scope="col"><span className="visually-hidden">Actions</span></th>
                    </tr>
                    </thead>
                    <tbody>
                    {users.map((user) => (
                        // The whole row opens the details for mouse users; the username button does it for keyboards.
                        <tr key={user.id} className="users-table-row" onClick={() => onSelect(user)}>
                            <td>{user.name ?? ''}</td>
                            <td>
                                <button
                                    className="table-link-button"
                                    onClick={(e) => {
                                        e.stopPropagation()
                                        onSelect(user)
                                    }}
                                >
                                    {user.username}
                                    <span className="visually-hidden"> (show details)</span>
                                </button>
                            </td>
                            <td className="voting-options-actions">
                                <button
                                    className="voting-options-edit"
                                    onClick={(e) => {
                                        e.stopPropagation()
                                        onEdit(user)
                                    }}
                                >
                                    Edit<span className="visually-hidden"> {user.username}</span>
                                </button>
                                <button
                                    className="voting-options-remove"
                                    onClick={(e) => {
                                        e.stopPropagation()
                                        onDelete(user)
                                    }}
                                >
                                    Remove<span className="visually-hidden"> {user.username}</span>
                                </button>
                            </td>
                        </tr>
                    ))}
                    </tbody>
                </table>
            </div>

            <Pagination page={page} totalPages={totalPages} onPageChange={onPageChange} />
        </>
    )
}

export default UsersTable
