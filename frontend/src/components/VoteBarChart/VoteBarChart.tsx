type BarDatum = {
    name: string
    count: number
}

type VoteBarChartProps = {
    data: BarDatum[]
}

function VoteBarChart({ data }: VoteBarChartProps) {
    const maxCount = Math.max(...data.map((d) => d.count), 1)

    return (
        <>
            {/* Screen readers get a plain list; the bars below are visual only. */}
            <ul className="visually-hidden">
                {data.map((d) => (
                    <li key={d.name}>
                        {d.name}: {d.count} {d.count === 1 ? 'vote' : 'votes'}
                    </li>
                ))}
            </ul>
            <div className="vote-bar-chart" aria-hidden="true">
                {data.map((d) => (
                    <div className="vote-bar-column" key={d.name}>
                        <span className="vote-bar-count">{d.count}</span>
                        <div className="vote-bar" style={{ height: `${(d.count / maxCount) * 100}%` }} />
                        <span className="vote-bar-label">{d.name}</span>
                    </div>
                ))}
            </div>
        </>
    )
}

export default VoteBarChart
