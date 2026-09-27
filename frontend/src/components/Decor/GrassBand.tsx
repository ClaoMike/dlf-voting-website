import './Decor.css'

const WIDTH = 1200
const HEIGHT = 110
const TONES = ['#00a650', '#3dbb74', '#8fd5a8']

// Deterministic "random" so the grass looks the same on every render.
function noise(i: number) {
    const x = Math.sin(i * 12.9898) * 43758.5453
    return x - Math.floor(x)
}

// Back rows are lighter and shorter; each blade is a thin curved leaf leaning left or right.
const BLADES = TONES.flatMap((_, row) => {
    const layer = TONES.length - 1 - row
    return Array.from({ length: 70 }, (_, i) => {
        const seed = row * 1000 + i
        const x = (i + noise(seed)) * (WIDTH / 70)
        const height = HEIGHT * (0.35 + 0.2 * layer + 0.35 * noise(seed + 1)) * (layer === 0 ? 0.75 : 1)
        const lean = (noise(seed + 2) - 0.5) * 60
        const base = 4 + 4 * noise(seed + 3)
        const tipX = x + lean
        const tipY = HEIGHT - height
        const d = `M${x - base} ${HEIGHT} Q${x - base / 2 + lean / 3} ${HEIGHT - height / 2} ${tipX} ${tipY} ` +
            `Q${x + base / 2 + lean / 3} ${HEIGHT - height / 2} ${x + base} ${HEIGHT} Z`
        return { d, fill: TONES[layer], tipY }
    })
})

// The tallest blades rise above HEIGHT, so start the view at the highest tip to keep every blade whole.
const TOP = Math.floor(Math.min(0, ...BLADES.map((blade) => blade.tipY)))

/** Decorative strip of grass along the bottom of the voter pages; it scales with the width, never cropped. */
function GrassBand() {
    return (
        <svg
            className="grass-band"
            viewBox={`0 ${TOP} ${WIDTH} ${HEIGHT - TOP}`}
            preserveAspectRatio="none"
            style={{ aspectRatio: `${WIDTH} / ${HEIGHT - TOP}` }}
            aria-hidden="true"
            focusable="false"
        >
            {BLADES.map((blade, i) => (
                <path key={i} d={blade.d} fill={blade.fill} />
            ))}
        </svg>
    )
}

export default GrassBand
