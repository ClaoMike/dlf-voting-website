type ProgressRingProps = {
    value: number
    max: number
    label: string
    size?: number
    strokeWidth?: number
}

function ProgressRing({ value, max, label, size = 160, strokeWidth = 14 }: ProgressRingProps) {
    const radius = (size - strokeWidth) / 2
    const circumference = 2 * Math.PI * radius
    const fraction = max > 0 ? Math.min(value / max, 1) : 0
    const offset = circumference * (1 - fraction)

    return (
        <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`}>
            <circle
                cx={size / 2}
                cy={size / 2}
                r={radius}
                fill="none"
                stroke="#e5e4e7"
                strokeWidth={strokeWidth}
            />
            <circle
                cx={size / 2}
                cy={size / 2}
                r={radius}
                fill="none"
                stroke="var(--accent)"
                strokeWidth={strokeWidth}
                strokeDasharray={circumference}
                strokeDashoffset={offset}
                strokeLinecap="round"
                transform={`rotate(-90 ${size / 2} ${size / 2})`}
                style={{ transition: 'stroke-dashoffset 0.5s ease' }}
            />
            <text
                x="50%"
                y="50%"
                textAnchor="middle"
                dominantBaseline="middle"
                fontSize={size * 0.15}
                fontWeight={600}
            >
                {label}
            </text>
        </svg>
    )
}

export default ProgressRing