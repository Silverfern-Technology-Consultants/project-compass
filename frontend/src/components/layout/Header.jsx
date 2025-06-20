import React from 'react';
import { Settings } from 'lucide-react';

const Header = ({ currentPage }) => {
    const getPageTitle = (page) => {
        switch (page) {
            case 'dashboard': return { title: 'Dashboard', subtitle: 'Azure Governance Assessment Portal' };
            case 'assessments': return { title: 'Assessments', subtitle: 'Manage and monitor your Azure governance assessments' };
            case 'reports': return { title: 'Reports', subtitle: 'View and manage your assessment reports and analytics' };
            case 'compliance': return { title: 'Compliance', subtitle: 'Monitor compliance status and requirements' };
            case 'team': return { title: 'Team Management', subtitle: 'Manage your team members and their access permissions' };
            case 'settings': return { title: 'Settings', subtitle: 'Configure your account and preferences' };
            default: return { title: 'Dashboard', subtitle: 'Azure Governance Assessment Portal' };
        }
    };

    const pageInfo = getPageTitle(currentPage);

    return (
        <header className="bg-gray-900 border-b border-gray-800 px-6 py-4">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-xl font-semibold text-white">{pageInfo.title}</h1>
                    <p className="text-sm text-gray-400">{pageInfo.subtitle}</p>
                </div>
                <div className="flex items-center space-x-4">
                    <div className="bg-green-500 text-black px-3 py-1 rounded text-sm font-medium">
                        Active Subscription
                    </div>
                    <button className="p-2 rounded hover:bg-gray-800 text-gray-400 hover:text-white">
                        <Settings size={20} />
                    </button>
                </div>
            </div>
        </header>
    );
};

export default Header;