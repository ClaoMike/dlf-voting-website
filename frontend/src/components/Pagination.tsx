type PaginationProps = {
    page: number
    totalPages: number
    onPageChange: (newPage: number) => void
}

function Pagination({ page, totalPages, onPageChange }: PaginationProps) {
    return (
        <nav className="users-pagination" aria-label="Pages">
            <button disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
                Previous<span className="visually-hidden"> page</span>
            </button>
            <span aria-current="page">
                Page {page} of {totalPages}
            </span>
            <button disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
                Next<span className="visually-hidden"> page</span>
            </button>
        </nav>
    )
}

export default Pagination
