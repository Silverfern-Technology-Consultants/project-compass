import React, { useState } from 'react';
import Layout from './components/layout/Layout';
import LandingPage from './LandingPage';

// Import page components
import DashboardPage from './components/pages/DashboardPage';
import AssessmentsPage from './components/pages/AssessmentsPage';
import ReportsPage from './components/pages/ReportsPage';
import TeamManagementPage from './components/pages/TeamManagementPage';
import CompliancePage from './components/pages/CompliancePage';
import SettingsPage from './components/pages/SettingsPage';

const App = () => {
    const [currentPage, setCurrentPage] = useState('dashboard');
    const [showLanding, setShowLanding] = useState(false); // Set to true to show landing page

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

export default App;