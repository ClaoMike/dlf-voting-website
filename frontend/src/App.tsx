import { useEffect, useState } from 'react'

type WeatherForecast = {
    date: string
    temperatureC: number
    temperatureF: number
    summary: string
}

function App() {
    const [forecast, setForecast] = useState<WeatherForecast[] | null>(null)
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        fetch('http://localhost:5120/weatherforecast')
            .then((res) => {
                if (!res.ok) throw new Error(`Status ${res.status}`)
                return res.json()
            })
            .then(setForecast)
            .catch((err) => setError(err.message))
    }, [])

    if (error) return <div>Error: {error}</div>
    if (!forecast) return <div>Loading...</div>

    return (
        <div>
            <h1>Backend connection test</h1>
            <ul>
                {forecast.map((f, i) => (
                    <li key={i}>
                        {f.date}: {f.temperatureC}°C, {f.summary}
                    </li>
                ))}
            </ul>
        </div>
    )
}

export default App