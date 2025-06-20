import React, { useState } from 'react';
import { Shield } from 'lucide-react';

const EditMemberModal = ({ isOpen, onClose, onSave, member }) => {
    const [role, setRole] = useState(member?.role || 'Member');

    if (!isOpen || !member) return null;

    const handleSubmit = (e) => {
        e.preventDefault();
        onSave({ ...member, role });
        onClose();
    };

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded w-full max-w-md mx-4">
                <div className="p-6 border-b border-gray-800">
                    <h2 className="text-xl font-semibold text-white">Edit Team Member</h2>
                    <p className="text-gray-400 text-sm mt-1">{member.name} ({member.email})</p>
                </div>

                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-2">Role</label>
                        <select
                            value={role}
                            onChange={(e) => setRole(e.target.value)}
                            className="w-full bg-gray-800 border border-gray-700 rounded px-3 py-2 text-white focus:outline-none focus:border-yellow-600"
                        >
                            <option value="Member">Member - Can run assessments and view reports</option>
                            <option value="Admin">Admin - Full access except team management</option>
                            <option value="Viewer">Viewer - Read-only access to reports</option>
                        </select>
                    </div>

                    <div className="bg-gray-800 border border-gray-700 rounded p-4">
                        <h4 className="font-medium text-white mb-2">Role Permissions</h4>
                        <div className="space-y-2 text-sm">
                            {role === 'Admin' && (
                                <>
                                    <div className="flex items-center space-x-2 text-green-400">
                                        <Shield size={14} />
                                        <span>Full access to all assessments and reports</span>
                                    </div>
                                    <div className="flex items-center space-x-2 text-green-400">
                                        <Shield size={14} />
                                        <span>Can modify settings and preferences</span>
                                    </div>
                                </>
                            )}
                            {role === 'Member' && (
                                <>
                                    <div className="flex items-center space-x-2 text-blue-400">
                                        <Shield size={14} />
                                        <span>Can run assessments and view reports</span>
                                    </div>
                                    <div className="flex items-center space-x-2 text-blue-400">
                                        <Shield size={14} />
                                        <span>Can export and share reports</span>
                                    </div>
                                </>
                            )}
                            {role === 'Viewer' && (
                                <div className="flex items-center space-x-2 text-gray-400">
                                    <Shield size={14} />
                                    <span>Read-only access to completed reports</span>
                                </div>
                            )}
                        </div>
                    </div>

                    <div className="flex items-center justify-end space-x-3 pt-4">
                        <button
                            type="button"
                            onClick={onClose}
                            className="px-4 py-2 text-gray-300 hover:text-white transition-colors"
                        >
                            Cancel
                        </button>
                        <button
                            type="submit"
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            Save Changes
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

export default EditMemberModal;