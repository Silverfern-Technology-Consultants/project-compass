import React from 'react';
import { Home, Settings, Users, FileText, BarChart3, Shield, ChevronLeft, ChevronRight, Crown } from 'lucide-react';

const Sidebar = ({ isOpen, setIsOpen, currentPage, setCurrentPage }) => {
    const menuItems = [
        { icon: Home, label: 'Dashboard', path: 'dashboard' },
        { icon: BarChart3, label: 'Assessments', path: 'assessments' },
        { icon: FileText, label: 'Reports', path: 'reports' },
        { icon: Shield, label: 'Compliance', path: 'compliance' },
        { icon: Users, label: 'Team Management', path: 'team' },
        { icon: Settings, label: 'Settings', path: 'settings' },
    ];

    return (
        <div className={`bg-gray-900 border-r border-gray-800 transition-all duration-300 ${isOpen ? 'w-64' : 'w-16'} flex flex-col`}>
            {/* Logo Section */}
            <div className="p-4 border-b border-gray-800">
                <div className="flex items-center justify-between">
                    {isOpen && (
                        <div className="flex items-center space-x-3">
                            <div className="w-8 h-8 bg-gradient-to-br from-yellow-400 to-yellow-600 rounded flex items-center justify-center">
                                <span className="text-black font-bold text-sm">C</span>
                            </div>
                            <span className="text-white font-semibold">Compass</span>
                        </div>
                    )}
                    <button
                        onClick={() => setIsOpen(!isOpen)}
                        className="p-1 rounded hover:bg-gray-800 text-gray-400 hover:text-white"
                    >
                        {isOpen ? <ChevronLeft size={20} /> : <ChevronRight size={20} />}
                    </button>
                </div>
            </div>

            {/* Navigation */}
            <nav className="flex-1 p-4">
                <ul className="space-y-2">
                    {menuItems.map((item, index) => (
                        <li key={index}>
                            <button
                                onClick={() => setCurrentPage(item.path)}
                                className={`w-full flex items-center space-x-3 p-3 rounded transition-colors ${currentPage === item.path
                                        ? 'bg-yellow-600 text-black'
                                        : 'text-gray-300 hover:bg-gray-800 hover:text-white'
                                    }`}
                            >
                                <item.icon size={20} />
                                {isOpen && <span>{item.label}</span>}
                            </button>
                        </li>
                    ))}
                </ul>
            </nav>

            {/* User Profile */}
            {isOpen && (
                <div className="p-4 border-t border-gray-800">
                    <div className="flex items-center space-x-3">
                        <div className="w-8 h-8 bg-yellow-600 rounded flex items-center justify-center">
                            <span className="text-black font-medium text-sm">JS</span>
                        </div>
                        <div className="flex-1 min-w-0">
                            <p className="text-sm font-medium text-white truncate">John Smith</p>
                            <p className="text-xs text-gray-400 truncate">Admin</p>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default Sidebar;