type SproutIconProps = {
    size?: number
}

/** A seedling: two leaves on a stem. Decorative, so hidden from screen readers. */
function SproutIcon({ size = 48 }: SproutIconProps) {
    return (
        <svg width={size} height={size} viewBox="0 0 48 48" aria-hidden="true" focusable="false">
            <circle cx="24" cy="24" r="24" fill="var(--accent-soft)" />
            <path d="M24 38V22" stroke="var(--accent-strong)" strokeWidth="2.5" strokeLinecap="round" />
            <path d="M24 24C24 16 18 12 11 12C11 20 16 24 24 24Z" fill="var(--accent)" />
            <path d="M24 28C24 21 29 17 36 17C36 24 31 28 24 28Z" fill="var(--accent-strong)" />
            <path d="M16 38H32" stroke="var(--accent-strong)" strokeWidth="2.5" strokeLinecap="round" />
        </svg>
    )
}

export default SproutIcon
