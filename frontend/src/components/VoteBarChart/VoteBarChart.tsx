import type { CSSProperties } from 'react'

type BarDatum = {
    id: string
    name: string
    count: number
}

type VoteBarChartProps = {
    data: BarDatum[]
}

/**
 * Two grid rows: the bars (all standing on the same baseline, the x-axis) and their labels below. Keeping the labels
 * out of the bars' row means a long option name can't push its bar up or shrink it.
 * With many options the chart scrolls sideways inside its card instead of widening the page.
 */
function VoteBarChart({ data }: VoteBarChartProps) {
    if (data.length === 0) return <p className="vote-bar-empty">No voting options yet.</p>

    const maxCount = Math.max(...data.map((d) => d.count), 1)
    const gridStyle = { '--bar-columns': data.length } as CSSProperties

    return (
        <>
            {/* Screen readers get a plain list; the bars below are visual only. */}
            <ul className="visually-hidden">
                {data.map((d) => (
                    <li key={d.id}>
                        {d.name}: {d.count} {d.count === 1 ? 'vote' : 'votes'}
                    </li>
                ))}
            </ul>
            <div className="vote-bar-chart" aria-hidden="true">
                <div className="vote-bar-grid" style={gridStyle}>
                    {data.map((d) => (
                        <div className="vote-bar-plot" key={`bar-${d.id}`}>
                            <div className="vote-bar" style={{ height: `${(d.count / maxCount) * 100}%` }}>
                                <span className="vote-bar-count">{d.count}</span>
                            </div>
                        </div>
                    ))}
                    {data.map((d) => (
                        <span className="vote-bar-label" key={`label-${d.id}`}>{d.name}</span>
                    ))}
                </div>
            </div>
        </>
    )
}

export default VoteBarChart
