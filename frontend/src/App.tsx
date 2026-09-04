import { Routes, Route } from 'react-router-dom'
import Layout from './components/Layout'

function Home() {
    return <h1>Home</h1>
}

function App() {
    return (
        <Routes>
            <Route element={<Layout />}>
                <Route path="/" element={<Home />} />
            </Route>
        </Routes>
    )
}

export default App