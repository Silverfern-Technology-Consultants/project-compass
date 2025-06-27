import React from 'react';
import { useClient } from '../../contexts/ClientContext';
import CompanyDashboard from './Dashboards/CompanyDashboard';
import ClientDashboard from './Dashboards/ClientDashboard';
import InternalDashboard from './Dashboards/InternalDashboard';

const DashboardPage = () => {
    const { selectedClient, isInternalSelected } = useClient();

    // Route to appropriate dashboard based on client selection
    if (!selectedClient) {
        return <CompanyDashboard />;
    }

    if (isInternalSelected()) {
        return <InternalDashboard />;
    }

    return <ClientDashboard client={selectedClient} />;
};

export default DashboardPage;