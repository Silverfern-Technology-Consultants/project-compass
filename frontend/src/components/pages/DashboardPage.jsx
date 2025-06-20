import React from 'react';
import { BarChart3, Shield, Users, FileText } from 'lucide-react';
import StatsCard from '../ui/StatsCard';

const DashboardPage = () => {
  const RecentAssessment = ({ name, status, score, date }) => (
    <div className="flex items-center justify-between p-4 bg-gray-900 border border-gray-800 rounded">
      <div className="flex-1">
        <h3 className="font-medium text-white">{name}</h3>
        <p className="text-sm text-gray-400">{date}</p>
      </div>
      <div className="flex items-center space-x-4">
        <div className="text-right">
          <p className="font-medium text-white">{score}%</p>
          <p className="text-sm text-gray-400">Score</p>
        </div>
        <div className={`px-3 py-1 rounded text-sm font-medium ${
          status === 'Completed' ? 'bg-green-600 text-white' : 
          status === 'In Progress' ? 'bg-yellow-600 text-black' : 
          'bg-gray-600 text-white'
        }`}>
          {status}
        </div>
      </div>
    </div>
  );

  const stats = [
    { title: 'Total Assessments', value: '24', change: '12', icon: BarChart3 },
    { title: 'Active Environments', value: '8', change: '5', icon: Shield },
    { title: 'Team Members', value: '6', change: '20', icon: Users },
    { title: 'Compliance Score', value: '94%', change: '3', icon: FileText },
  ];

  const recentAssessments = [
    { name: 'Production Environment', status: 'Completed', score: 94, date: '2 hours ago' },
    { name: 'Development Environment', status: 'In Progress', score: 78, date: '1 day ago' },
    { name: 'Staging Environment', status: 'Completed', score: 89, date: '3 days ago' },
    { name: 'Testing Environment', status: 'Pending', score: null, date: '5 days ago' },
  ];

  return (
    <div className="space-y-6">
      {/* Stats Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {stats.map((stat, index) => (
          <StatsCard key={index} {...stat} />
        ))}
      </div>

      {/* Recent Assessments */}
      <div className="bg-gray-950 border border-gray-800 rounded">
        <div className="p-6 border-b border-gray-800">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold text-white">Recent Assessments</h2>
            <button className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors">
              New Assessment
            </button>
          </div>
        </div>
        <div className="p-6">
          <div className="space-y-4">
            {recentAssessments.map((assessment, index) => (
              <RecentAssessment key={index} {...assessment} />
            ))}
          </div>
        </div>
      </div>

      {/* Quick Actions */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="bg-gray-900 border border-gray-800 rounded p-6">
          <h3 className="font-semibold text-white mb-4">Quick Start</h3>
          <div className="space-y-3">
            <button className="w-full text-left p-3 bg-gray-800 hover:bg-gray-700 rounded transition-colors text-white">
              Run Full Assessment
            </button>
            <button className="w-full text-left p-3 bg-gray-800 hover:bg-gray-700 rounded transition-colors text-white">
              Naming Convention Check
            </button>
            <button className="w-full text-left p-3 bg-gray-800 hover:bg-gray-700 rounded transition-colors text-white">
              Tagging Compliance
            </button>
          </div>
        </div>

        <div className="bg-gray-900 border border-gray-800 rounded p-6">
          <h3 className="font-semibold text-white mb-4">Team Activity</h3>
          <div className="space-y-3 text-sm">
            <div className="flex items-center space-x-3">
              <div className="w-2 h-2 bg-green-500 rounded-full"></div>
              <span className="text-gray-300">Sarah completed Production assessment</span>
            </div>
            <div className="flex items-center space-x-3">
              <div className="w-2 h-2 bg-yellow-500 rounded-full"></div>
              <span className="text-gray-300">Mike started Dev environment review</span>
            </div>
            <div className="flex items-center space-x-3">
              <div className="w-2 h-2 bg-blue-500 rounded-full"></div>
              <span className="text-gray-300">New team member invited</span>
            </div>
          </div>
        </div>

        <div className="bg-gray-900 border border-gray-800 rounded p-6">
          <h3 className="font-semibold text-white mb-4">Compliance Status</h3>
          <div className="space-y-3">
            <div className="flex justify-between items-center">
              <span className="text-gray-300">Naming Conventions</span>
              <span className="text-green-400 font-medium">96%</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-300">Tagging Strategy</span>
              <span className="text-yellow-400 font-medium">84%</span>
            </div>
            <div className="flex justify-between items-center">
              <span className="text-gray-300">Resource Organization</span>
              <span className="text-green-400 font-medium">92%</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default DashboardPage;