import { Routes, Route } from 'react-router-dom'
import Layout from './components/Layout'
import { AdminProtectedRoute, UserProtectedRoute } from './components/ProtectedRoute'
import AdminLogin from './pages/AdminLogin'
import UserLogin from './pages/UserLogin'
import Welcome from './pages/Welcome'
import AdminOverview from './pages/admin/AdminOverview'
import AdminVotingOptions from './pages/admin/AdminVotingOptions'
import AdminUsers from './pages/admin/AdminUsers'
import AdminAdministrators from './pages/admin/AdminAdministrators'
import AdminSettings from './pages/admin/AdminSettings'
import {VOTING_SYSTEM_WEBSITE_TITLE} from "./constants/strings.ts";
import {useEffect} from "react";

function App() {

    useEffect(() => {
        document.title = VOTING_SYSTEM_WEBSITE_TITLE
    }, [])
    
    return (
        <Routes>
            <Route element={<Layout />}>
                <Route path="/" element={<UserLogin />} />
                <Route path="/login" element={<UserLogin />} />
                <Route path="/login/admin" element={<AdminLogin />} />

                <Route element={<UserProtectedRoute />}>
                    <Route path="/welcome" element={<Welcome />} />
                </Route>

                <Route element={<AdminProtectedRoute />}>
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