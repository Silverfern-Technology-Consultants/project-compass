import React from 'react';

const StatsCard = ({ title, value, change, icon: Icon, color = "yellow" }) => {
    const getIconBgColor = (color) => {
        switch (color) {
            case 'green': return 'bg-green-600';
            case 'blue': return 'bg-blue-600';
            case 'red': return 'bg-red-600';
            case 'purple': return 'bg-purple-600';
            default: return 'bg-yellow-600';
        }
    };

    const getIconTextColor = (color) => {
        switch (color) {
            case 'yellow': return 'text-black';
            default: return 'text-white';
        }
    };

    return (
        <div className="bg-gray-900 border border-gray-800 rounded p-6">
            <div className="flex items-center justify-between">
                <div>
                    <p className="text-sm font-medium text-gray-400">{title}</p>
                    <p className="text-2xl font-bold text-white">{value}</p>
                    {change && (
                        <p className="text-sm text-green-400">+{change}% from last month</p>
                    )}
                </div>
                <div className={`p-3 ${getIconBgColor(color)} rounded`}>
                    <Icon size={24} className={getIconTextColor(color)} />
                </div>
            </div>
        </div>
    );
};

export default StatsCard;