import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { Settings, LogOut, ChevronDown, User, Moon, Sun } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useClient } from '../../contexts/ClientContext';
import { useLayout } from '../../contexts/LayoutContext';
import ClientSelector from '../ui/ClientSelector';

const Header = ({ dropdownOpen, setDropdownOpen, clientDropdownOpen, setClientDropdownOpen, onOutsideClick }) => {
    const { user, logout } = useAuth();
    const { selectedClient, getClientDisplayName, isInternalSelected } = useClient();
    const { sidebarOpen } = useLayout();
    const navigate = useNavigate();
    const location = useLocation();
    const [isDarkMode, setIsDarkMode] = useState(true);

    // Extract current page from pathname
    const currentPage = location.pathname.split('/').pop() || 'dashboard';

    const getPageTitle = (page) => {
        const clientSuffix = selectedClient ? ` - ${getClientDisplayName()}` : '';

        switch (page) {
            case 'dashboard':
                return {
                    title: `Dashboard${clientSuffix}`,
                    subtitle: selectedClient
                        ? `Cloud governance monitoring for ${getClientDisplayName()}`
                        : 'Cloud governance monitoring and assessment platform'
                };
            case 'assessments':
                return {
                    title: `Assessments${clientSuffix}`,
                    subtitle: selectedClient
                        ? `Manage and monitor assessments for ${getClientDisplayName()}`
                        : 'Manage and monitor your cloud governance assessments'
                };
            case 'reports':
                return {
                    title: `Reports${clientSuffix}`,
                    subtitle: selectedClient
                        ? `View assessment reports and analytics for ${getClientDisplayName()}`
                        : 'View and manage your assessment reports and analytics'
                };
            case 'compliance':
                return {
                    title: `Compliance${clientSuffix}`,
                    subtitle: selectedClient
                        ? `Monitor compliance status for ${getClientDisplayName()}`
                        : 'Monitor compliance status and requirements'
                };
            case 'team':
                return {
                    title: 'Team Management',
                    subtitle: 'Manage your team members and their access permissions'
                };
            case 'clients':
                return {
                    title: 'My Clients',
                    subtitle: 'Manage your MSP clients and their assessments'
                };
            case 'settings':
                return {
                    title: 'Company Settings',
                    subtitle: 'Configure your organization and preferences'
                };
            case 'profile':
                return {
                    title: 'My Settings',
                    subtitle: 'Manage your account information and preferences'
                };
            default:
                return {
                    title: `Dashboard${clientSuffix}`,
                    subtitle: 'Cloud governance monitoring and assessment platform'
                };
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
        const firstName = user.FirstName || '';
        const lastName = user.LastName || '';
        return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
    };

    const getUserDisplayName = () => {
        if (!user) return 'User';
        // Use PascalCase properties
        const firstName = user.FirstName || '';
        const lastName = user.LastName || '';
        return `${firstName} ${lastName}`.trim() || user.Email || 'User';
    };

    const getSubscriptionDisplay = () => {
        if (!user) return { text: 'Loading...', className: 'bg-gray-600 text-gray-300' };

        // Use PascalCase property
        const subscriptionStatus = user.SubscriptionStatus;

        switch (subscriptionStatus) {
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
    };

    const subscriptionDisplay = getSubscriptionDisplay();

    return (
        <header
            className={`fixed top-0 right-0 bg-gray-900 border-b border-gray-800 px-6 py-4 transition-all duration-300 z-30 h-[88px] ${sidebarOpen ? 'left-64' : 'left-20'
                }`}
        >
            <div className="flex items-center justify-between h-full">
                {/* Page Title Section - Simplified since breadcrumbs will show navigation */}
                <div className="flex-1 flex flex-col justify-center">
                    <h1 className="text-xl font-semibold text-white leading-tight">{pageInfo.title}</h1>
                    <p className="text-sm text-gray-400 leading-tight">{pageInfo.subtitle}</p>
                </div>

                {/* Right Side Controls */}
                <div className="flex items-center space-x-4">
                    {/* Client Selector - Only show on relevant pages */}
                    {!['team', 'clients', 'settings', 'profile'].includes(currentPage) && (
                        <ClientSelector
                            dropdownOpen={clientDropdownOpen}
                            setDropdownOpen={setClientDropdownOpen}
                            onOutsideClick={onOutsideClick}
                        />
                    )}

                    {/* Subscription Status Badge */}
                    <div className={`px-2 py-1 rounded text-xs font-medium ${subscriptionDisplay.className}`}>
                        {subscriptionDisplay.text}
                    </div>

                    {/* User Dropdown */}
                    <div className="relative">
                        <button
                            onClick={() => setDropdownOpen(!dropdownOpen)}
                            className="flex items-center space-x-2 p-2 rounded hover:bg-gray-800 text-gray-300 hover:text-white transition-colors"
                        >
                            <div className="w-7 h-7 bg-yellow-600 rounded flex items-center justify-center">
                                <span className="text-black font-medium text-xs">{getUserInitials()}</span>
                            </div>
                            <div className="hidden md:block text-left">
                                <p className="text-xs font-medium text-white">{getUserDisplayName()}</p>
                                <p className="text-xs text-gray-400">{user?.CompanyName || 'Company'}</p>
                            </div>
                            <ChevronDown size={14} className={`transition-transform ${dropdownOpen ? 'rotate-180' : ''}`} />
                        </button>

                        {/* Dropdown Menu */}
                        {dropdownOpen && (
                            <>
                                {/* Backdrop - Closes on outside click */}
                                <div
                                    className="fixed inset-0 z-10"
                                    onClick={onOutsideClick}
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
                                            <span>My Settings</span>
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