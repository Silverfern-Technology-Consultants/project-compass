import React, { useState } from 'react';
import Header from './Header';
import Sidebar from './Sidebar';
import { useLocation } from 'react-router-dom';
import { useLayout } from '../../contexts/LayoutContext';

const Layout = ({ children }) => {
    const location = useLocation();
    const { sidebarOpen } = useLayout(); // Get sidebar state
    const [headerDropdownOpen, setHeaderDropdownOpen] = useState(false);
    const [clientDropdownOpen, setClientDropdownOpen] = useState(false);

    // Extract current page from pathname for sidebar highlighting
    const getCurrentPage = () => {
        const pathname = location.pathname;

        // Handle company routes
        if (pathname.includes('/company/clients')) return 'clients';
        if (pathname.includes('/company/team')) return 'team';
        if (pathname.includes('/company/settings')) return 'company-settings';

        // Handle regular routes
        const page = pathname.split('/').pop();
        return page || 'dashboard';
    };

    const currentPage = getCurrentPage();

    // Function to close all dropdowns - this will be passed to both components
    const closeAllDropdowns = () => {
        setHeaderDropdownOpen(false);
        setClientDropdownOpen(false);
    };

    return (
        <div className="min-h-screen bg-gray-950">
            <Sidebar
                currentPage={currentPage}
                onSidebarClick={closeAllDropdowns}
            />
            <Header
                dropdownOpen={headerDropdownOpen}
                setDropdownOpen={setHeaderDropdownOpen}
                clientDropdownOpen={clientDropdownOpen}
                setClientDropdownOpen={setClientDropdownOpen}
                onOutsideClick={closeAllDropdowns}
            />
            <main
                className={`transition-all duration-300 pt-20 p-6 ${sidebarOpen ? 'ml-64' : 'ml-20'
                    }`}
            >
                {children}
            </main>
        </div>
    );
};

export default Layout;