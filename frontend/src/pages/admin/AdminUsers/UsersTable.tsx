import type { User } from './types'

type UsersTableProps = {
    users: User[]
    page: number
    totalPages: number
    onEdit: (user: User) => void
    onDelete: (user: User) => void
    onPageChange: (newPage: number) => void
}

function UsersTable({ users, page, totalPages, onEdit, onDelete, onPageChange }: UsersTableProps) {
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
                {users.map((user) => (
                    <tr key={user.id}>
                        <td>{user.email}</td>
                        <td>{new Date(user.createdAt).toLocaleDateString()}</td>
                        <td className="voting-options-actions">
                            <button className="voting-options-edit" onClick={() => onEdit(user)}>
                                Edit
                            </button>
                            <button className="voting-options-remove" onClick={() => onDelete(user)}>
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