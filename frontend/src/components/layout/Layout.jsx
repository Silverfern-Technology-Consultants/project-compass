import React, { useState } from 'react';
import { Shield } from 'lucide-react';
import Sidebar from './Sidebar';
import Header from './Header';

// Mock auth context for demo
const useAuth = () => ({
    user: { name: "John Smith", email: "john@safehaven.com", role: "Admin" },
    hasSubscription: true
});

const Layout = ({ children, currentPage, setCurrentPage }) => {
    const [sidebarOpen, setSidebarOpen] = useState(true);
    const { user, hasSubscription } = useAuth();

    // Subscription check
    if (!hasSubscription) {
        return (
            <div className="min-h-screen bg-gray-950 flex items-center justify-center">
                <div className="bg-gray-900 border border-gray-800 rounded p-8 max-w-md w-full mx-4">
                    <div className="text-center">
                        <div className="w-16 h-16 bg-gradient-to-br from-yellow-400 to-yellow-600 rounded mx-auto mb-4 flex items-center justify-center">
                            <Shield size={32} className="text-black" />
                        </div>
                        <h2 className="text-xl font-semibold text-white mb-2">Subscription Required</h2>
                        <p className="text-gray-400 mb-6">
                            You need an active subscription to access the Compass assessment portal.
                        </p>
                        <button className="w-full bg-yellow-600 hover:bg-yellow-700 text-black font-medium py-2 px-4 rounded transition-colors">
                            Subscribe Now
                        </button>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-gray-950 flex">
            <Sidebar
                isOpen={sidebarOpen}
                setIsOpen={setSidebarOpen}
                currentPage={currentPage}
                setCurrentPage={setCurrentPage}
            />

            <div className="flex-1 flex flex-col">
                <Header currentPage={currentPage} />

                <main className="flex-1 p-6">
                    {children}
                </main>
            </div>
        </div>
    );
};

export default Layout;