import type { VotingOption } from './types'

type VotingOptionsTableProps = {
    options: VotingOption[]
    onEdit: (option: VotingOption) => void
    onDelete: (option: VotingOption) => void
}

function VotingOptionsTable({ options, onEdit, onDelete }: VotingOptionsTableProps) {
    return (
        <div className="table-scroll">
            <table className="voting-options-table">
                <caption className="visually-hidden">Voting options</caption>
                <thead>
                <tr>
                    <th scope="col">Name</th>
                    <th scope="col">Created</th>
                    <th scope="col"><span className="visually-hidden">Actions</span></th>
                </tr>
                </thead>
                <tbody>
                {options.map((option) => (
                    <tr key={option.id}>
                        <td>{option.name}</td>
                        <td>{new Date(option.createdAt).toLocaleDateString()}</td>
                        <td className="voting-options-actions">
                            <button className="voting-options-edit" onClick={() => onEdit(option)}>
                                Edit<span className="visually-hidden"> {option.name}</span>
                            </button>
                            <button className="voting-options-remove" onClick={() => onDelete(option)}>
                                Remove<span className="visually-hidden"> {option.name}</span>
                            </button>
                        </td>
                    </tr>
                ))}
                </tbody>
            </table>
        </div>
    )
}

export default VotingOptionsTable
