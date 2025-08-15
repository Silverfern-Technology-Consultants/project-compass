import React, { useState } from 'react';
import { Settings, Shield, ChevronDown, ChevronRight, Wrench, Database, BarChart3 } from 'lucide-react';
import { Link, useLocation } from 'react-router-dom';

const ToolsMenu = ({ isCollapsed }) => {
  const [isExpanded, setIsExpanded] = useState(false);
  const location = useLocation();

  const toolsItems = [
    {
      name: 'Permissions Checker',
      href: '/tools/permissions',
      icon: Shield,
      description: 'Check Azure permissions across all clients'
    },
    {
      name: 'System Health',
      href: '/tools/health',
      icon: Database,
      description: 'Monitor system health and connectivity'
    },
    {
      name: 'Usage Analytics',
      href: '/tools/analytics',
      icon: BarChart3,
      description: 'View platform usage statistics'
    }
  ];

  const isToolsRoute = location.pathname.startsWith('/tools');

  if (isCollapsed) {
    return (
      <div className="relative group">
        <Link
          to="/tools/permissions"
          className={`flex items-center justify-center w-full p-3 rounded-lg transition-colors ${
            isToolsRoute 
              ? 'bg-blue-100 text-blue-700' 
              : 'text-gray-700 hover:bg-gray-100'
          }`}
          title="Tools"
        >
          <Wrench className="h-5 w-5" />
        </Link>
        
        {/* Tooltip */}
        <div className="absolute left-full ml-2 top-0 bg-gray-900 text-white text-sm rounded-lg px-2 py-1 opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none whitespace-nowrap z-50">
          Tools
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-1">
      <button
        onClick={() => setIsExpanded(!isExpanded)}
        className={`flex items-center justify-between w-full p-3 rounded-lg transition-colors ${
          isToolsRoute 
            ? 'bg-blue-100 text-blue-700' 
            : 'text-gray-700 hover:bg-gray-100'
        }`}
      >
        <div className="flex items-center space-x-3">
          <Wrench className="h-5 w-5" />
          <span className="font-medium">Tools</span>
        </div>
        {isExpanded ? (
          <ChevronDown className="h-4 w-4" />
        ) : (
          <ChevronRight className="h-4 w-4" />
        )}
      </button>

      {isExpanded && (
        <div className="ml-4 space-y-1">
          {toolsItems.map((item) => {
            const Icon = item.icon;
            const isActive = location.pathname === item.href;
            
            return (
              <Link
                key={item.name}
                to={item.href}
                className={`flex items-center space-x-3 p-2 rounded-lg transition-colors ${
                  isActive
                    ? 'bg-blue-50 text-blue-700 border-l-2 border-blue-700'
                    : 'text-gray-600 hover:bg-gray-50 hover:text-gray-900'
                }`}
                title={item.description}
              >
                <Icon className="h-4 w-4" />
                <div>
                  <div className="text-sm font-medium">{item.name}</div>
                  <div className="text-xs text-gray-500">{item.description}</div>
                </div>
              </Link>
            );
          })}
        </div>
      )}
    </div>
  );
};

export default ToolsMenu;
