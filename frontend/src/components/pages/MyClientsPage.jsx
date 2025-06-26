import React from 'react';
import { Building2, Clock, Users } from 'lucide-react';

const MyClientsPage = () => {
    return (
        <div className="space-y-6">
            {/* Header */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-white">My Clients</h1>
                    <p className="text-gray-400">Manage your MSP clients and their assessments</p>
                </div>
            </div>

            {/* Coming Soon Card */}
            <div className="bg-gray-900 border border-gray-800 rounded p-12 text-center">
                <div className="max-w-md mx-auto">
                    <div className="w-16 h-16 bg-yellow-600 rounded-full flex items-center justify-center mx-auto mb-6">
                        <Building2 size={32} className="text-black" />
                    </div>

                    <h2 className="text-xl font-semibold text-white mb-3">Client Management Coming Soon</h2>
                    <p className="text-gray-400 mb-6">
                        We're building powerful client management features to help you organize
                        and track assessments for each of your MSP clients.
                    </p>

                    {/* Feature Preview */}
                    <div className="bg-gray-800 border border-gray-700 rounded p-4 text-left">
                        <h3 className="text-sm font-semibold text-white mb-3">What's Coming:</h3>
                        <div className="space-y-2 text-sm text-gray-300">
                            <div className="flex items-center space-x-2">
                                <div className="w-1.5 h-1.5 bg-yellow-600 rounded-full"></div>
                                <span>Organize Azure subscriptions by client</span>
                            </div>
                            <div className="flex items-center space-x-2">
                                <div className="w-1.5 h-1.5 bg-yellow-600 rounded-full"></div>
                                <span>Client-specific assessment reports</span>
                            </div>
                            <div className="flex items-center space-x-2">
                                <div className="w-1.5 h-1.5 bg-yellow-600 rounded-full"></div>
                                <span>Individual client dashboards</span>
                            </div>
                            <div className="flex items-center space-x-2">
                                <div className="w-1.5 h-1.5 bg-yellow-600 rounded-full"></div>
                                <span>Client access management</span>
                            </div>
                        </div>
                    </div>

                    <div className="mt-6 flex items-center justify-center space-x-2 text-sm text-gray-500">
                        <Clock size={16} />
                        <span>Currently in development</span>
                    </div>
                </div>
            </div>

            {/* Temporary Stats */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Total Clients</p>
                            <p className="text-2xl font-bold text-white">-</p>
                            <p className="text-xs text-gray-500 mt-1">Feature pending</p>
                        </div>
                        <Building2 size={24} className="text-gray-600" />
                    </div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Active Subscriptions</p>
                            <p className="text-2xl font-bold text-white">-</p>
                            <p className="text-xs text-gray-500 mt-1">Feature pending</p>
                        </div>
                        <Users size={24} className="text-gray-600" />
                    </div>
                </div>

                <div className="bg-gray-900 border border-gray-800 rounded p-6">
                    <div className="flex items-center justify-between">
                        <div>
                            <p className="text-sm text-gray-400">Recent Assessments</p>
                            <p className="text-2xl font-bold text-white">-</p>
                            <p className="text-xs text-gray-500 mt-1">Feature pending</p>
                        </div>
                        <Clock size={24} className="text-gray-600" />
                    </div>
                </div>
            </div>
        </div>
    );
};

export default MyClientsPage;