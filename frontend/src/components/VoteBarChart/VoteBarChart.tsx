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
        <div className="vote-bar-chart">
            {data.map((d) => (
                <div className="vote-bar-column" key={d.name}>
                    <span className="vote-bar-count">{d.count}</span>
                    <div className="vote-bar" style={{ height: `${(d.count / maxCount) * 100}%` }} />
                    <span className="vote-bar-label">{d.name}</span>
                </div>
            ))}
        </div>
    )
}

export default VoteBarChart