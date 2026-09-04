import { Routes, Route } from 'react-router-dom'
import Layout from './components/Layout'
import AdminLogin from './pages/AdminLogin'

function Home() {
    return <h1>Home</h1>
}

function App() {
    return (
        <Routes>
            <Route element={<Layout />}>
                <Route path="/" element={<Home />} />
                <Route path="/login/admin" element={<AdminLogin />} />
            </Route>
        </Routes>
    )
}

export default App