import React, { useState } from 'react';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import Layout from './components/layout/Layout';
import LandingPage from './LandingPage';
import LoginPage from './components/pages/LoginPage';
import RegisterPage from './components/pages/RegisterPage';
import VerifyEmailPage from './components/pages/VerifyEmailPage';

// Import existing page components
import DashboardPage from './components/pages/DashboardPage';
import AssessmentsPage from './components/pages/AssessmentsPage';
import ReportsPage from './components/pages/ReportsPage';
import TeamManagementPage from './components/pages/TeamManagementPage';
import CompliancePage from './components/pages/CompliancePage';
import SettingsPage from './components/pages/SettingsPage';

const AuthenticatedApp = () => {
    const [currentPage, setCurrentPage] = useState('dashboard');
    const [showLanding, setShowLanding] = useState(false);

    // Show landing page if requested
    if (showLanding) {
        return <LandingPage />;
    }

    const renderCurrentPage = () => {
        switch (currentPage) {
            case 'dashboard':
                return <DashboardPage />;
            case 'assessments':
                return <AssessmentsPage />;
            case 'reports':
                return <ReportsPage />;
            case 'team':
                return <TeamManagementPage />;
            case 'compliance':
                return <CompliancePage />;
            case 'settings':
                return <SettingsPage />;
            default:
                return <DashboardPage />;
        }
    };

    return (
        <Layout currentPage={currentPage} setCurrentPage={setCurrentPage}>
            {renderCurrentPage()}
        </Layout>
    );
};

const UnauthenticatedApp = () => {
    const [currentView, setCurrentView] = useState('login'); // 'login', 'register', 'verify-email'
    const [verificationToken, setVerificationToken] = useState(null);

    const renderCurrentView = () => {
        switch (currentView) {
            case 'register':
                return <RegisterPage onSwitchToLogin={() => setCurrentView('login')} />;
            case 'verify-email':
                return <VerifyEmailPage
                    token={verificationToken}
                    onSwitchToLogin={() => setCurrentView('login')}
                />;
            case 'login':
            default:
                return <LoginPage
                    onSwitchToRegister={() => setCurrentView('register')}
                    onNeedVerification={(token) => {
                        setVerificationToken(token);
                        setCurrentView('verify-email');
                    }}
                />;
        }
    };

    return renderCurrentView();
};

const AppContent = () => {
    const { isAuthenticated, isLoading, user } = useAuth();

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

    // Check if email verification is required
    if (isAuthenticated && user && !user.emailVerified) {
        return <VerifyEmailPage />;
    }

    // Render authenticated or unauthenticated app
    return isAuthenticated ? <AuthenticatedApp /> : <UnauthenticatedApp />;
};

const App = () => {
    return (
        <AuthProvider>
            <AppContent />
        </AuthProvider>
    );
};

export default App;