import React from 'react';
import { useLocation } from 'react-router-dom';
import Header from './Header';
import Sidebar from './Sidebar';
import { useLayout } from '../../contexts/LayoutContext';

const Layout = ({ children }) => {
    const location = useLocation();
    const { sidebarOpen } = useLayout();

    // Extract current page from pathname
    const currentPage = location.pathname.split('/').pop() || 'dashboard';

    return (
        <div className="min-h-screen bg-gray-950">
            <Sidebar currentPage={currentPage} />
            <Header />
            {/* Main content with dynamic margins that respond to sidebar state */}
            <main className={`pt-24 p-6 transition-all duration-300 ${sidebarOpen ? 'ml-64' : 'ml-20'
                }`}>
                {children}
            </main>
        </div>
    );
};

export default Layout;