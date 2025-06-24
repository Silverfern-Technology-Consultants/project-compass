import React from 'react';
import { BrowserRouter as Router, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { LayoutProvider } from './contexts/LayoutContext';
import Layout from './components/layout/Layout';
import ProtectedRoute from './components/ProtectedRoute';
// Core components
import DashboardPage from './components/pages/DashboardPage';
import AssessmentsPage from './components/pages/AssessmentsPage';
// Authentication components
import LoginPage from './components/pages/LoginPage';
import RegisterPage from './components/pages/RegisterPage';
import VerifyEmailPage from './components/pages/VerifyEmailPage';
import AcceptInvitePage from './components/pages/AcceptInvitePage'; // NEW IMPORT
// Other page components
import ReportsPage from './components/pages/ReportsPage';
import TeamManagementPage from './components/pages/TeamManagementPage';
import CompliancePage from './components/pages/CompliancePage';
import SettingsPage from './components/pages/SettingsPage';
import ProfilePage from './components/pages/ProfilePage';
// Landing page
import LandingPage from './LandingPage';
// MFA Components
import MfaVerificationModal from './components/modals/MfaVerificationModal';
import MfaSetupModal from './components/modals/MfaSetupModal';

const AuthenticatedRoutes = () => {
    return (
        <LayoutProvider>
            <Layout>
                <Routes>
                    <Route path="/dashboard" element={<DashboardPage />} />
                    <Route path="/assessments" element={<AssessmentsPage />} />
                    <Route path="/reports" element={<ReportsPage />} />
                    <Route path="/team" element={<TeamManagementPage />} />
                    <Route path="/compliance" element={<CompliancePage />} />
                    <Route path="/settings" element={<SettingsPage />} />
                    <Route path="/profile" element={<ProfilePage />} />
                    <Route path="/" element={<Navigate to="/dashboard" replace />} />
                    <Route path="*" element={<Navigate to="/dashboard" replace />} />
                </Routes>
            </Layout>
        </LayoutProvider>
    );
};

const AppContent = () => {
    const {
        isAuthenticated,
        isLoading,
        mfaRequired,
        mfaSetupRequired,
        completeMfaVerification,
        completeMfaSetup
    } = useAuth();

    const handleMfaVerificationSuccess = (result) => {
        completeMfaVerification(result);
    };

    const handleMfaSetupComplete = () => {
        completeMfaSetup();
    };

    // Show loading spinner while checking authentication
    if (isLoading) {
        return (
            <div className="min-h-screen flex items-center justify-center bg-gray-950">
                <div className="flex flex-col items-center">
                    <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-yellow-600"></div>
                    <p className="mt-4 text-gray-400">Loading...</p>
                </div>
            </div>
        );
    }

    return (
        <>
            <Routes>
                {/* Public routes */}
                <Route path="/" element={<LandingPage />} />
                <Route path="/login" element={<LoginPage />} />
                <Route path="/register" element={<RegisterPage />} />
                <Route path="/verify-email" element={<VerifyEmailPage />} />
                <Route path="/accept-invite" element={<AcceptInvitePage />} /> {/* NEW ROUTE */}

                {/* Protected routes */}
                <Route
                    path="/app/*"
                    element={
                        <ProtectedRoute>
                            <AuthenticatedRoutes />
                        </ProtectedRoute>
                    }
                />

                {/* Redirect authenticated users from auth pages */}
                {isAuthenticated && (
                    <>
                        <Route path="/login" element={<Navigate to="/app/dashboard" replace />} />
                        <Route path="/register" element={<Navigate to="/app/dashboard" replace />} />
                    </>
                )}

                {/* Catch all - redirect to landing or dashboard */}
                <Route
                    path="*"
                    element={
                        <Navigate
                            to={isAuthenticated ? "/app/dashboard" : "/"}
                            replace
                        />
                    }
                />
            </Routes>

            {/* Global MFA Modals */}
            <MfaVerificationModal
                isOpen={mfaRequired}
                onClose={() => { }} // Prevent closing during login flow
                onVerificationSuccess={handleMfaVerificationSuccess}
            />

            <MfaSetupModal
                isOpen={mfaSetupRequired}
                onClose={() => { }} // Prevent closing during setup flow
                onSetupComplete={handleMfaSetupComplete}
            />
        </>
    );
};

const App = () => {
    return (
        <AuthProvider>
            <Router>
                <AppContent />
            </Router>
        </AuthProvider>
    );
};

export default App;