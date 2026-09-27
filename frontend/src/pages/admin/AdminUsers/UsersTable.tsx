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
    return (
        <>
            <table className="voting-options-table">
                <thead>
                <tr>
                    <th>Name</th>
                    <th>Username</th>
                    <th></th>
                </tr>
                </thead>
                <tbody>
                {users.map((user) => (
                    <tr key={user.id} className="users-table-row" onClick={() => onSelect(user)}>
                        <td>{user.name ?? ''}</td>
                        <td>{user.username}</td>
                        <td className="voting-options-actions">
                            <button
                                className="voting-options-edit"
                                onClick={(e) => {
                                    e.stopPropagation()
                                    onEdit(user)
                                }}
                            >
                                Edit
                            </button>
                            <button
                                className="voting-options-remove"
                                onClick={(e) => {
                                    e.stopPropagation()
                                    onDelete(user)
                                }}
                            >
                                Remove
                            </button>
                        </td>
                    </tr>
                ))}
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

export default UsersTable
