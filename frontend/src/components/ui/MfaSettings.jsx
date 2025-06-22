import React, { useState, useEffect } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import MfaSetupModal from '../modals/MfaSetupModal';

const MfaSettings = () => {
    const [mfaStatus, setMfaStatus] = useState(null);
    const [loading, setLoading] = useState(true);
    const [showSetupModal, setShowSetupModal] = useState(false);
    const [showDisableModal, setShowDisableModal] = useState(false);
    const [showRegenerateModal, setShowRegenerateModal] = useState(false);
    const [error, setError] = useState('');

    const { getMfaStatus, disableMfa, regenerateBackupCodes } = useAuth();

    useEffect(() => {
        loadMfaStatus();
    }, []);

    const loadMfaStatus = async () => {
        try {
            setLoading(true);
            const status = await getMfaStatus();
            setMfaStatus(status);
        } catch (err) {
            setError('Failed to load MFA status');
        } finally {
            setLoading(false);
        }
    };

    const handleSetupComplete = () => {
        setShowSetupModal(false);
        loadMfaStatus(); // Refresh status
    };

    if (loading) {
        return (
            <div className="bg-gray-900 border border-gray-800 rounded p-6">
                <div className="animate-pulse space-y-4">
                    <div className="h-4 bg-gray-700 rounded w-1/4"></div>
                    <div className="h-20 bg-gray-700 rounded"></div>
                </div>
            </div>
        );
    }

    return (
        <div className="bg-gray-900 border border-gray-800 rounded p-6">
            <h3 className="text-lg font-semibold text-white mb-4">
                Two-Factor Authentication
            </h3>

            {error && (
                <div className="bg-red-900 border border-red-800 text-red-300 px-4 py-3 rounded mb-4">
                    {error}
                </div>
            )}

            <div className="space-y-6">
                <div className="flex items-center justify-between">
                    <div>
                        <p className="text-sm font-medium text-white">
                            Status: {mfaStatus?.isMfaEnabled ? (
                                <span className="text-green-400">Enabled</span>
                            ) : (
                                <span className="text-gray-400">Disabled</span>
                            )}
                        </p>
                        <p className="text-sm text-gray-400">
                            {mfaStatus?.isMfaEnabled
                                ? 'Two-factor authentication is protecting your account'
                                : 'Add an extra layer of security to your account'
                            }
                        </p>
                    </div>

                    {mfaStatus?.isMfaEnabled ? (
                        <button
                            onClick={() => setShowDisableModal(true)}
                            className="bg-red-600 hover:bg-red-700 text-white px-4 py-2 rounded font-medium transition-colors"
                        >
                            Disable 2FA
                        </button>
                    ) : (
                        <button
                            onClick={() => setShowSetupModal(true)}
                            className="bg-yellow-600 hover:bg-yellow-700 text-black px-4 py-2 rounded font-medium transition-colors"
                        >
                            Enable 2FA
                        </button>
                    )}
                </div>

                {mfaStatus?.isMfaEnabled && (
                    <>
                        <div className="border-t border-gray-800 pt-6">
                            <div className="flex items-center justify-between">
                                <div>
                                    <p className="text-sm font-medium text-white">Backup Codes</p>
                                    <p className="text-sm text-gray-400">
                                        {mfaStatus.backupCodesRemaining} of 10 codes remaining
                                    </p>
                                </div>
                                <button
                                    onClick={() => setShowRegenerateModal(true)}
                                    className="bg-gray-600 hover:bg-gray-700 text-white px-4 py-2 rounded font-medium transition-colors"
                                >
                                    Regenerate Codes
                                </button>
                            </div>

                            {mfaStatus.backupCodesRemaining <= 3 && (
                                <div className="mt-2 bg-yellow-900 border border-yellow-800 text-yellow-300 px-3 py-2 rounded text-sm">
                                    ⚠️ You have {mfaStatus.backupCodesRemaining} backup codes remaining. Consider regenerating new codes.
                                </div>
                            )}
                        </div>

                        {mfaStatus.mfaSetupDate && (
                            <div className="border-t border-gray-800 pt-6">
                                <p className="text-sm text-gray-400">
                                    Enabled on: {new Date(mfaStatus.mfaSetupDate).toLocaleDateString()}
                                </p>
                                {mfaStatus.lastMfaUsedDate && (
                                    <p className="text-sm text-gray-400">
                                        Last used: {new Date(mfaStatus.lastMfaUsedDate).toLocaleDateString()}
                                    </p>
                                )}
                            </div>
                        )}
                    </>
                )}
            </div>

            {/* Setup Modal */}
            <MfaSetupModal
                isOpen={showSetupModal}
                onClose={() => setShowSetupModal(false)}
                onSetupComplete={handleSetupComplete}
            />

            {/* Disable MFA Modal */}
            <DisableMfaModal
                isOpen={showDisableModal}
                onClose={() => setShowDisableModal(false)}
                onDisableComplete={() => {
                    setShowDisableModal(false);
                    loadMfaStatus();
                }}
            />

            {/* Regenerate Backup Codes Modal */}
            <RegenerateCodesModal
                isOpen={showRegenerateModal}
                onClose={() => setShowRegenerateModal(false)}
                onRegenerateComplete={() => {
                    setShowRegenerateModal(false);
                    loadMfaStatus();
                }}
            />
        </div>
    );
};

// Disable MFA Modal Component
const DisableMfaModal = ({ isOpen, onClose, onDisableComplete }) => {
    const [password, setPassword] = useState('');
    const [mfaCode, setMfaCode] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const { disableMfa } = useAuth();

    const handleDisable = async () => {
        if (!password || !mfaCode) {
            setError('Please fill in all fields');
            return;
        }

        try {
            setLoading(true);
            setError('');
            await disableMfa(password, mfaCode);
            setPassword('');
            setMfaCode('');
            onDisableComplete();
        } catch (err) {
            setError(err.message);
        } finally {
            setLoading(false);
        }
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded-lg p-6 w-full max-w-md mx-4">
                <h3 className="text-lg font-medium text-white mb-4">Disable Two-Factor Authentication</h3>

                {error && (
                    <div className="bg-red-900 border border-red-800 text-red-300 px-4 py-3 rounded mb-4">
                        {error}
                    </div>
                )}

                <div className="space-y-4">
                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-1">
                            Current Password
                        </label>
                        <input
                            type="password"
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-md text-white focus:outline-none focus:ring-2 focus:ring-yellow-500"
                        />
                    </div>

                    <div>
                        <label className="block text-sm font-medium text-gray-300 mb-1">
                            Authenticator Code
                        </label>
                        <input
                            type="text"
                            placeholder="000000"
                            value={mfaCode}
                            onChange={(e) => setMfaCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                            className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-md text-white focus:outline-none focus:ring-2 focus:ring-yellow-500"
                        />
                    </div>

                    <div className="bg-yellow-900 border border-yellow-800 text-yellow-300 px-4 py-3 rounded">
                        ⚠️ Disabling 2FA will make your account less secure. Are you sure you want to continue?
                    </div>

                    <div className="flex space-x-3">
                        <button
                            onClick={onClose}
                            className="flex-1 bg-gray-700 hover:bg-gray-600 text-white py-2 px-4 rounded transition-colors"
                        >
                            Cancel
                        </button>
                        <button
                            onClick={handleDisable}
                            disabled={loading}
                            className="flex-1 bg-red-600 hover:bg-red-700 text-white py-2 px-4 rounded disabled:opacity-50 transition-colors"
                        >
                            {loading ? 'Disabling...' : 'Disable 2FA'}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

// Regenerate Backup Codes Modal Component
const RegenerateCodesModal = ({ isOpen, onClose, onRegenerateComplete }) => {
    const [mfaCode, setMfaCode] = useState('');
    const [newCodes, setNewCodes] = useState(null);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const { regenerateBackupCodes } = useAuth();

    const handleRegenerate = async () => {
        if (!mfaCode) {
            setError('Please enter your authenticator code');
            return;
        }

        try {
            setLoading(true);
            setError('');
            const result = await regenerateBackupCodes(mfaCode);
            setNewCodes(result.backupCodes);
        } catch (err) {
            setError(err.message);
        } finally {
            setLoading(false);
        }
    };

    const downloadCodes = () => {
        if (!newCodes) return;

        const content = `Compass MFA Backup Codes\nGenerated: ${new Date().toISOString()}\n\n${newCodes.join('\n')}\n\nKeep these codes safe! Each can only be used once.`;
        const blob = new Blob([content], { type: 'text/plain' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'compass-backup-codes.txt';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    };

    const handleClose = () => {
        setMfaCode('');
        setNewCodes(null);
        setError('');
        onClose();
    };

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded-lg p-6 w-full max-w-md mx-4">
                <h3 className="text-lg font-medium text-white mb-4">Regenerate Backup Codes</h3>

                {error && (
                    <div className="bg-red-900 border border-red-800 text-red-300 px-4 py-3 rounded mb-4">
                        {error}
                    </div>
                )}

                {!newCodes ? (
                    <div className="space-y-4">
                        <p className="text-sm text-gray-300">
                            Enter your authenticator code to generate new backup codes. This will invalidate all existing backup codes.
                        </p>

                        <input
                            type="text"
                            placeholder="000000"
                            value={mfaCode}
                            onChange={(e) => setMfaCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                            className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-md text-white text-center focus:outline-none focus:ring-2 focus:ring-yellow-500"
                        />

                        <div className="flex space-x-3">
                            <button
                                onClick={handleClose}
                                className="flex-1 bg-gray-700 hover:bg-gray-600 text-white py-2 px-4 rounded transition-colors"
                            >
                                Cancel
                            </button>
                            <button
                                onClick={handleRegenerate}
                                disabled={loading || mfaCode.length !== 6}
                                className="flex-1 bg-yellow-600 hover:bg-yellow-700 text-black py-2 px-4 rounded disabled:opacity-50 transition-colors"
                            >
                                {loading ? 'Generating...' : 'Generate New Codes'}
                            </button>
                        </div>
                    </div>
                ) : (
                    <div className="space-y-4">
                        <div className="bg-green-900 border border-green-800 text-green-300 px-4 py-3 rounded">
                            ✓ New backup codes generated successfully!
                        </div>

                        <div>
                            <p className="text-sm text-gray-300 mb-3">
                                Save these new backup codes in a safe place. Your old codes are now invalid.
                            </p>

                            <div className="bg-gray-800 border border-gray-700 rounded p-3 mb-3">
                                <div className="grid grid-cols-2 gap-2 text-sm font-mono text-white">
                                    {newCodes.map((code, index) => (
                                        <div key={index} className="text-center">{code}</div>
                                    ))}
                                </div>
                            </div>

                            <button
                                onClick={downloadCodes}
                                className="w-full bg-gray-600 hover:bg-gray-700 text-white py-2 px-4 rounded transition-colors mb-3"
                            >
                                Download Backup Codes
                            </button>
                        </div>

                        <button
                            onClick={() => {
                                onRegenerateComplete();
                                handleClose();
                            }}
                            className="w-full bg-yellow-600 hover:bg-yellow-700 text-black py-2 px-4 rounded transition-colors"
                        >
                            Done
                        </button>
                    </div>
                )}
            </div>
        </div>
    );
};

export default MfaSettings;