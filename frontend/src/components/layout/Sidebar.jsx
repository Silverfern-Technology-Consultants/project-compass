import React, { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
    Home,
    FileText,
    BarChart3,
    Shield,
    Users,
    Settings,
    ChevronLeft,
    ChevronRight,
    ChevronDown,
    User,
    LogOut,
    Moon,
    Sun,
    Building2,
    UserCheck
} from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useLayout } from '../../contexts/LayoutContext';

const Sidebar = ({ currentPage, onSidebarClick }) => {
    const navigate = useNavigate();
    const { user, logout } = useAuth();
    const { sidebarOpen, toggleSidebar } = useLayout();
    const [userMenuOpen, setUserMenuOpen] = useState(false);
    const [companyMenuOpen, setCompanyMenuOpen] = useState(false);
    const [isDarkMode, setIsDarkMode] = useState(true);
    const [activeTooltip, setActiveTooltip] = useState(null);
    const [tooltipPosition, setTooltipPosition] = useState({ top: 0, left: 0 });

    const menuItems = [
        { id: 'dashboard', label: 'Dashboard', icon: Home, path: '/app/dashboard' },
        { id: 'assessments', label: 'Assessments', icon: FileText, path: '/app/assessments' },
        { id: 'reports', label: 'Reports', icon: BarChart3, path: '/app/reports' },
        { id: 'compliance', label: 'Compliance', icon: Shield, path: '/app/compliance' },
        { id: 'settings', label: 'My Settings', icon: Settings, path: '/app/settings' },
    ];

    const companyMenuItems = [
        { id: 'clients', label: 'My Clients', icon: Building2, path: '/app/company/clients' },
        { id: 'team', label: 'My Team', icon: Users, path: '/app/company/team' },
        { id: 'company-settings', label: 'Company Settings', icon: Settings, path: '/app/company/settings' },
    ];

    const getUserInitials = () => {
        if (!user) return 'U';
        const firstName = user.FirstName || '';
        const lastName = user.LastName || '';
        return `${firstName.charAt(0)}${lastName.charAt(0)}`.toUpperCase();
    };

    const getUserDisplayName = () => {
        if (!user) return 'User';
        const firstName = user.FirstName || '';
        const lastName = user.LastName || '';
        return `${firstName} ${lastName}`.trim() || user.Email || 'User';
    };

    const getUserRole = () => {
        if (!user) return 'User';
        const subscriptionStatus = user.SubscriptionStatus;
        if (subscriptionStatus === 'Active') return 'Admin';
        if (subscriptionStatus === 'Trial') return 'Trial User';
        return 'User';
    };

    const handleLogout = () => {
        logout();
        setUserMenuOpen(false);
    };

    const handleNavigation = (path, settingsTab = null) => {
        setUserMenuOpen(false);
        if (path.includes('settings') && settingsTab) {
            localStorage.setItem('settingsTab', settingsTab);
        }
        navigate(path);
    };

    const handleTooltipShow = (event, label) => {
        if (!sidebarOpen) {
            const rect = event.currentTarget.getBoundingClientRect();
            setTooltipPosition({
                top: rect.top + rect.height / 2,
                left: 84
            });
            setActiveTooltip(label);
        }
    };

    const handleTooltipHide = () => {
        setActiveTooltip(null);
    };

    const toggleDarkMode = () => {
        setIsDarkMode(!isDarkMode);
        console.log('Dark mode toggled:', !isDarkMode);
    };

    const isCompanyPageActive = () => {
        return companyMenuItems.some(item => currentPage === item.id);
    };

    const getOrganizationName = () => {
        // Try multiple possible sources for organization name
        const orgName = user?.companyName || user?.CompanyName || user?.organizationName || user?.OrganizationName;
        return orgName || 'My Company'; // Fallback to 'My Company' if no name found
    };

    // Handle clicks on the sidebar to close header dropdown
    const handleSidebarClick = (e) => {
        // Call the parent's click handler to close header dropdown
        if (onSidebarClick) {
            onSidebarClick();
        }
    };

    return (
        <>
            <div
                className={`bg-gray-900 border-r border-gray-800 transition-all duration-300 ${sidebarOpen ? 'w-64' : 'w-20'} flex flex-col fixed left-0 top-0 h-screen z-40`}
                onClick={handleSidebarClick}
            >
                {/* Header */}
                <div className="p-4 border-b border-gray-800">
                    <div className="flex items-center justify-between">
                        {sidebarOpen && (
                            <div className="flex items-center space-x-3">
                                <div className="w-8 h-8 bg-gradient-to-br from-yellow-400 to-yellow-600 rounded flex items-center justify-center">
                                    <span className="text-black font-bold text-sm">C</span>
                                </div>
                                <span className="text-white font-semibold">Compass</span>
                            </div>
                        )}
                        <button
                            onClick={toggleSidebar}
                            className="p-1 rounded hover:bg-gray-800 text-gray-400 hover:text-white"
                        >
                            {sidebarOpen ? <ChevronLeft size={20} /> : <ChevronRight size={20} />}
                        </button>
                    </div>
                </div>

                {/* Navigation */}
                <nav className="flex-1 p-4 overflow-y-auto">
                    <ul className="space-y-2">
                        {/* Main menu items */}
                        {menuItems.map((item) => (
                            <li key={item.id} className="relative">
                                <div className="relative">
                                    <button
                                        onClick={() => handleNavigation(item.path)}
                                        onMouseEnter={(e) => handleTooltipShow(e, item.label)}
                                        onMouseLeave={handleTooltipHide}
                                        className={`w-full flex items-center ${sidebarOpen ? 'space-x-3' : 'justify-center'} ${sidebarOpen ? 'px-3' : 'px-2'} py-4 rounded transition-colors ${currentPage === item.id
                                            ? 'bg-yellow-600 text-black'
                                            : 'text-gray-300 hover:bg-gray-800 hover:text-white'
                                            }`}
                                    >
                                        <item.icon size={sidebarOpen ? 20 : 26} />
                                        {sidebarOpen && <span>{item.label}</span>}
                                    </button>
                                </div>
                            </li>
                        ))}

                        {/* My Company Section */}
                        <li className="relative">
                            {sidebarOpen ? (
                                /* Expanded Company Menu */
                                <div>
                                    <button
                                        onClick={() => setCompanyMenuOpen(!companyMenuOpen)}
                                        className={`w-full flex items-center space-x-3 px-3 py-4 rounded transition-colors ${isCompanyPageActive() ? 'bg-yellow-600 text-black' : 'text-gray-300 hover:bg-gray-800 hover:text-white'
                                            }`}
                                    >
                                        <Building2 size={20} />
                                        <span className="flex-1 text-left">{getOrganizationName()}</span>
                                        <ChevronDown
                                            size={16}
                                            className={`transition-transform ${companyMenuOpen ? 'rotate-180' : ''}`}
                                        />
                                    </button>

                                    {/* Company Submenu */}
                                    {companyMenuOpen && (
                                        <ul className="mt-2 ml-6 space-y-1 border-l border-gray-700 pl-4">
                                            {companyMenuItems.map((item) => (
                                                <li key={item.id}>
                                                    <button
                                                        onClick={() => handleNavigation(item.path)}
                                                        className={`w-full flex items-center space-x-3 px-3 py-2 rounded transition-colors ${currentPage === item.id
                                                            ? 'bg-yellow-600 text-black'
                                                            : 'text-gray-400 hover:bg-gray-800 hover:text-white'
                                                            }`}
                                                    >
                                                        <item.icon size={16} />
                                                        <span className="text-sm">{item.label}</span>
                                                    </button>
                                                </li>
                                            ))}
                                        </ul>
                                    )}
                                </div>
                            ) : (
                                /* Collapsed Company Menu */
                                <div className="relative">
                                    <button
                                        onClick={() => setCompanyMenuOpen(!companyMenuOpen)}
                                        onMouseEnter={(e) => handleTooltipShow(e, getOrganizationName())}
                                        onMouseLeave={handleTooltipHide}
                                        className={`w-full flex justify-center px-2 py-4 rounded transition-colors ${isCompanyPageActive() ? 'bg-yellow-600 text-black' : 'text-gray-300 hover:bg-gray-800 hover:text-white'
                                            }`}
                                    >
                                        <Building2 size={26} />
                                    </button>

                                    {/* Collapsed Company Submenu */}
                                    {companyMenuOpen && (
                                        <div className="absolute left-full top-0 ml-2 w-48 bg-gray-800 border border-gray-700 rounded-md shadow-lg z-50">
                                            <div className="py-1">
                                                <div className="px-3 py-2 border-b border-gray-700">
                                                    <p className="text-xs font-medium text-white">{getOrganizationName()}</p>
                                                </div>
                                                {companyMenuItems.map((item) => (
                                                    <button
                                                        key={item.id}
                                                        onClick={() => {
                                                            handleNavigation(item.path);
                                                            setCompanyMenuOpen(false);
                                                        }}
                                                        className={`w-full text-left px-3 py-2 text-sm hover:bg-gray-700 flex items-center space-x-2 ${currentPage === item.id ? 'text-yellow-400 bg-gray-700' : 'text-gray-300 hover:text-white'
                                                            }`}
                                                    >
                                                        <item.icon size={14} />
                                                        <span>{item.label}</span>
                                                    </button>
                                                ))}
                                            </div>
                                        </div>
                                    )}
                                </div>
                            )}
                        </li>
                    </ul>
                </nav>

                {/* User Profile Section */}
                <div className="p-4 border-t border-gray-800">
                    {sidebarOpen ? (
                        /* Expanded Profile Menu */
                        <div className="relative">
                            <button
                                onClick={() => setUserMenuOpen(!userMenuOpen)}
                                className="w-full flex items-center space-x-3 p-2 rounded hover:bg-gray-800 transition-colors"
                            >
                                <div className="w-8 h-8 bg-yellow-600 rounded flex items-center justify-center">
                                    <span className="text-black font-medium text-sm">{getUserInitials()}</span>
                                </div>
                                <div className="flex-1 min-w-0 text-left">
                                    <p className="text-sm font-medium text-white truncate">{getUserDisplayName()}</p>
                                    <p className="text-xs text-gray-400 truncate">{getUserRole()}</p>
                                </div>
                                <ChevronDown size={16} className={`text-gray-400 transition-transform ${userMenuOpen ? 'rotate-180' : ''}`} />
                            </button>

                            {/* User Submenu */}
                            {userMenuOpen && (
                                <>
                                    {/* Backdrop for expanded sidebar user menu */}
                                    <div
                                        className="fixed inset-0 z-10"
                                        onClick={() => setUserMenuOpen(false)}
                                    />
                                    <div className="absolute bottom-full left-0 right-0 mb-2 bg-gray-800 border border-gray-700 rounded-md shadow-lg z-20">
                                        <div className="py-1">
                                            <div className="px-3 py-2 border-b border-gray-700">
                                                <p className="text-xs font-medium text-white">{getUserDisplayName()}</p>
                                                <p className="text-xs text-gray-400">{user?.email}</p>
                                            </div>

                                            <button
                                                onClick={() => handleNavigation('/app/settings')}
                                                className="w-full text-left px-3 py-2 text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-2"
                                            >
                                                <User size={14} />
                                                <span>Account Settings</span>
                                            </button>

                                            <button
                                                onClick={() => handleNavigation('/app/settings', 'preferences')}
                                                className="w-full text-left px-3 py-2 text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-2"
                                            >
                                                <Settings size={14} />
                                                <span>Preferences</span>
                                            </button>

                                            <button
                                                onClick={toggleDarkMode}
                                                className="w-full text-left px-3 py-2 text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-2"
                                            >
                                                {isDarkMode ? <Sun size={14} /> : <Moon size={14} />}
                                                <span>{isDarkMode ? 'Light Mode' : 'Dark Mode'}</span>
                                            </button>

                                            <div className="border-t border-gray-700 mt-1 pt-1">
                                                <button
                                                    onClick={handleLogout}
                                                    className="w-full text-left px-3 py-2 text-sm text-red-400 hover:bg-gray-700 hover:text-red-300 flex items-center space-x-2"
                                                >
                                                    <LogOut size={14} />
                                                    <span>Sign Out</span>
                                                </button>
                                            </div>
                                        </div>
                                    </div>
                                </>
                            )}
                        </div>
                    ) : (
                        /* Collapsed Profile Menu */
                        <div className="relative">
                            <div className="relative">
                                <button
                                    onClick={() => setUserMenuOpen(!userMenuOpen)}
                                    onMouseEnter={(e) => handleTooltipShow(e, getUserDisplayName())}
                                    onMouseLeave={handleTooltipHide}
                                    className="w-full flex justify-center p-2 rounded hover:bg-gray-800 transition-colors"
                                >
                                    <div className="w-8 h-8 bg-yellow-600 rounded flex items-center justify-center">
                                        <span className="text-black font-medium text-sm">{getUserInitials()}</span>
                                    </div>
                                </button>
                            </div>

                            {/* Collapsed User Submenu */}
                            {userMenuOpen && (
                                <>
                                    {/* Backdrop for collapsed sidebar user menu */}
                                    <div
                                        className="fixed inset-0 z-10"
                                        onClick={() => setUserMenuOpen(false)}
                                    />
                                    <div className="absolute bottom-full left-0 mb-2 w-48 bg-gray-800 border border-gray-700 rounded-md shadow-lg z-50">
                                        <div className="py-1">
                                            <div className="px-3 py-2 border-b border-gray-700">
                                                <p className="text-xs font-medium text-white">{getUserDisplayName()}</p>
                                                <p className="text-xs text-gray-400">{user?.email}</p>
                                            </div>

                                            <button
                                                onClick={() => handleNavigation('/app/settings')}
                                                className="w-full text-left px-3 py-2 text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-2"
                                            >
                                                <User size={14} />
                                                <span>Account Settings</span>
                                            </button>

                                            <button
                                                onClick={() => handleNavigation('/app/settings', 'preferences')}
                                                className="w-full text-left px-3 py-2 text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-2"
                                            >
                                                <Settings size={14} />
                                                <span>Preferences</span>
                                            </button>

                                            <button
                                                onClick={toggleDarkMode}
                                                className="w-full text-left px-3 py-2 text-sm text-gray-300 hover:bg-gray-700 hover:text-white flex items-center space-x-2"
                                            >
                                                {isDarkMode ? <Sun size={14} /> : <Moon size={14} />}
                                                <span>{isDarkMode ? 'Light Mode' : 'Dark Mode'}</span>
                                            </button>

                                            <div className="border-t border-gray-700 mt-1 pt-1">
                                                <button
                                                    onClick={handleLogout}
                                                    className="w-full text-left px-3 py-2 text-sm text-red-400 hover:bg-gray-700 hover:text-red-300 flex items-center space-x-2"
                                                >
                                                    <LogOut size={14} />
                                                    <span>Sign Out</span>
                                                </button>
                                            </div>
                                        </div>
                                    </div>
                                </>
                            )}
                        </div>
                    )}
                </div>
            </div>

            {/* Fixed positioned tooltip portal */}
            {activeTooltip && !sidebarOpen && (
                <div
                    className="fixed px-2 py-1 bg-gray-800 text-white text-sm rounded whitespace-nowrap z-50 pointer-events-none"
                    style={{
                        top: `${tooltipPosition.top}px`,
                        left: `${tooltipPosition.left}px`,
                        transform: 'translateY(-50%)'
                    }}
                >
                    {activeTooltip}
                </div>
            )}
        </>
    );
};

export default Sidebar;