import { Routes, Route } from 'react-router-dom'
import Layout from './components/Layout'
import ProtectedRoute from './components/ProtectedRoute'
import AdminLogin from './pages/AdminLogin'
import AdminOverview from './pages/admin/AdminOverview'
import AdminVotingOptions from './pages/admin/AdminVotingOptions'
import AdminUsers from './pages/admin/AdminUsers'
import AdminAdministrators from './pages/admin/AdminAdministrators'
import AdminSettings from './pages/admin/AdminSettings'

function Home() {
    return <h1>Home</h1>
}

function App() {
    return (
        <Routes>
            <Route element={<Layout />}>
                <Route path="/" element={<Home />} />
                <Route path="/login/admin" element={<AdminLogin />} />

                <Route element={<ProtectedRoute />}>
                    <Route path="/admin/overview" element={<AdminOverview />} />
                    <Route path="/admin/voting_options" element={<AdminVotingOptions />} />
                    <Route path="/admin/users" element={<AdminUsers />} />
                    <Route path="/admin/administrators" element={<AdminAdministrators />} />
                    <Route path="/admin/settings" element={<AdminSettings />} />
                </Route>
            </Route>
        </Routes>
    )
}

export default App