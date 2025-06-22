import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Settings, LogOut, ChevronDown, User, Moon, Sun } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useLayout } from '../../contexts/LayoutContext';

const Header = () => {
    const { user, logout } = useAuth();
    const { sidebarOpen } = useLayout();
    const navigate = useNavigate();
    const location = useLocation();
    const [dropdownOpen, setDropdownOpen] = useState(false);
    const [isDarkMode, setIsDarkMode] = useState(true);

    // Extract current page from pathname
    const currentPage = location.pathname.split('/').pop() || 'dashboard';

    const getPageTitle = (page) => {
        switch (page) {
            case 'dashboard': return { title: 'Dashboard', subtitle: 'Azure Governance Assessment Portal' };
            case 'assessments': return { title: 'Assessments', subtitle: 'Manage and monitor your Azure governance assessments' };
            case 'reports': return { title: 'Reports', subtitle: 'View and manage your assessment reports and analytics' };
            case 'compliance': return { title: 'Compliance', subtitle: 'Monitor compliance status and requirements' };
            case 'team': return { title: 'Team Management', subtitle: 'Manage your team members and their access permissions' };
            case 'settings': return { title: 'Settings', subtitle: 'Configure your account and preferences' };
            case 'profile': return { title: 'Account Settings', subtitle: 'Manage your account information and preferences' };
            default: return { title: 'Dashboard', subtitle: 'Azure Governance Assessment Portal' };
        }
    };

    const pageInfo = getPageTitle(currentPage);

    const handleLogout = () => {
        logout();
        setDropdownOpen(false);
    };

    const handleNavigation = (page, settingsTab = null) => {
        setDropdownOpen(false);
        // Store the settings tab in localStorage for SettingsPage to read
        if (page === 'settings' && settingsTab) {
            localStorage.setItem('settingsTab', settingsTab);
        }
        navigate(`/app/${page}`);
    };

    const getUserInitials = () => {
        if (!user) return 'U';
        const firstName = user.firstName || '';
        const lastName = user.lastName || '';
        return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
    };

    const getUserDisplayName = () => {
        if (!user) return 'User';
        return `${user.firstName || ''} ${user.lastName || ''}`.trim() || user.email || 'User';
    };

    const getSubscriptionDisplay = () => {
        if (!user) return { text: 'Loading...', className: 'bg-gray-600 text-gray-300' };

        switch (user.subscriptionStatus) {
            case 'Active':
                return { text: 'Active Subscription', className: 'bg-green-500 text-black' };
            case 'Trial':
                return { text: 'Trial Active', className: 'bg-yellow-600 text-black' };
            case 'Expired':
                return { text: 'Subscription Expired', className: 'bg-red-600 text-white' };
            default:
                return { text: 'No Subscription', className: 'bg-gray-600 text-gray-300' };
        }
    };

    const toggleDarkMode = () => {
        setIsDarkMode(!isDarkMode);
        console.log('Dark mode toggled:', !isDarkMode);
    };

    const subscriptionDisplay = getSubscriptionDisplay();

    return (
        <header
            className={`fixed top-0 right-0 bg-gray-900 border-b border-gray-800 px-6 py-4 transition-all duration-300 z-30 ${sidebarOpen ? 'left-64' : 'left-20'
                }`}
        >
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-xl font-semibold text-white">{pageInfo.title}</h1>
                    <p className="text-sm text-gray-400">{pageInfo.subtitle}</p>
                </div>

                <div className="flex items-center space-x-4">
                    {/* Subscription Status */}
                    <div className={`px-3 py-1 rounded text-sm font-medium ${subscriptionDisplay.className}`}>
                        {subscriptionDisplay.text}
                    </div>

                    {/* User Dropdown */}
                    <div className="relative">
                        <button
                            onClick={() => setDropdownOpen(!dropdownOpen)}
                            className="flex items-center space-x-3 p-2 rounded hover:bg-gray-800 text-gray-300 hover:text-white transition-colors"
                        >
                            <div className="w-8 h-8 bg-yellow-600 rounded flex items-center justify-center">
                                <span className="text-black font-medium text-sm">{getUserInitials()}</span>
                            </div>
                            <div className="hidden md:block text-left">
                                <p className="text-sm font-medium text-white">{getUserDisplayName()}</p>
                                <p className="text-xs text-gray-400">{user?.companyName || 'Company'}</p>
                            </div>
                            <ChevronDown size={16} className={`transition-transform ${dropdownOpen ? 'rotate-180' : ''}`} />
                        </button>

                        {/* Dropdown Menu */}
                        {dropdownOpen && (
                            <>
                                {/* Backdrop */}
                                <div
                                    className="fixed inset-0 z-10"
                                    onClick={() => setDropdownOpen(false)}
                                />

                                {/* Dropdown Content */}
                                <div className="absolute right-0 mt-2 w-48 bg-gray-800 border border-gray-700 rounded-md shadow-lg z-20">
                                    <div className="py-1">
                                        <div className="px-4 py-2 border-b border-gray-700">
                                            <p className="text-sm font-medium text-white">{getUserDisplayName()}</p>
                                            <p className="text-xs text-gray-400">{user?.email}</p>
                                        </div>

                                        <button
                                            onClick={() => handleNavigation('settings')}
                                            className="w-full text-left px-4 py-2 text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-2"
                                        >
                                            <User size={16} />
                                            <span>Account Settings</span>
                                        </button>

                                        <button
                                            onClick={() => handleNavigation('settings', 'preferences')}
                                            className="w-full text-left px-4 py-2 text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-2"
                                        >
                                            <Settings size={16} />
                                            <span>Preferences</span>
                                        </button>

                                        <button
                                            onClick={toggleDarkMode}
                                            className="w-full text-left px-4 py-2 text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-2"
                                        >
                                            {isDarkMode ? <Sun size={16} /> : <Moon size={16} />}
                                            <span>{isDarkMode ? 'Light Mode' : 'Dark Mode'}</span>
                                        </button>

                                        <div className="border-t border-gray-700 mt-1 pt-1">
                                            <button
                                                onClick={handleLogout}
                                                className="w-full text-left px-4 py-2 text-sm text-red-400 hover:bg-gray-700 hover:text-red-300 flex items-center space-x-2"
                                            >
                                                <LogOut size={16} />
                                                <span>Sign Out</span>
                                            </button>
                                        </div>
                                    </div>
                                </div>
                            </>
                        )}
                    </div>
                </div>
            </div>
        </header>
    );
};

export default Header;