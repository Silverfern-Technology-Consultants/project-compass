import React, { useState, useEffect, useCallback } from 'react';
import { useAuth } from '../../contexts/AuthContext';

const MfaSetupModal = ({ isOpen, onClose, onSetupComplete }) => {
    const [step, setStep] = useState(1); // 1: QR Code, 2: Verify, 3: Backup Codes
    const [setupData, setSetupData] = useState(null);
    const [totpCode, setTotpCode] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const { setupMfa, verifyMfaSetup } = useAuth();

    const handleSetupMfa = useCallback(async () => {
        try {
            setLoading(true);
            setError('');
            const data = await setupMfa();
            setSetupData(data);
        } catch (err) {
            setError(err.message);
        } finally {
            setLoading(false);
        }
    }, [setupMfa]);

    useEffect(() => {
        if (isOpen && step === 1) {
            handleSetupMfa();
        }
    }, [isOpen, step, handleSetupMfa]);

    const verifySetup = async () => {
        if (!totpCode || totpCode.length !== 6) {
            setError('Please enter a valid 6-digit code');
            return;
        }

        try {
            setLoading(true);
            setError('');
            await verifyMfaSetup(totpCode);
            setStep(3); // Show backup codes
        } catch (err) {
            setError(err.message);
        } finally {
            setLoading(false);
        }
    };

    const finishSetup = () => {
        setStep(1);
        setTotpCode('');
        setSetupData(null);
        setError('');
        onSetupComplete();
        onClose();
    };

    const downloadBackupCodes = () => {
        if (!setupData?.backupCodes) return;

        const content = `Compass MFA Backup Codes\nGenerated: ${new Date().toISOString()}\n\n${setupData.backupCodes.join('\n')}\n\nKeep these codes safe! Each can only be used once.`;
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

    if (!isOpen) return null;

    return (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
            <div className="bg-gray-900 border border-gray-800 rounded-lg p-6 w-full max-w-md mx-4">
                <div className="flex justify-between items-center mb-6">
                    <h2 className="text-xl font-semibold text-white">Set Up Two-Factor Authentication</h2>
                    <button
                        onClick={onClose}
                        className="text-gray-400 hover:text-gray-300"
                        disabled={loading}
                    >
                        ✕
                    </button>
                </div>

                {error && (
                    <div className="bg-red-900 border border-red-800 text-red-300 px-4 py-3 rounded mb-4">
                        {error}
                    </div>
                )}

                {step === 1 && (
                    <div className="space-y-4">
                        <div className="text-sm text-gray-300">
                            <p className="mb-3">Scan this QR code with your authenticator app:</p>
                            <ul className="list-disc list-inside space-y-1 mb-4 text-gray-400">
                                <li>Google Authenticator</li>
                                <li>Authy</li>
                                <li>Microsoft Authenticator</li>
                                <li>1Password</li>
                            </ul>
                        </div>

                        {loading ? (
                            <div className="flex justify-center py-8">
                                <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-yellow-600"></div>
                            </div>
                        ) : setupData?.qrCode ? (
                            <div className="flex flex-col items-center space-y-4">
                                <img
                                    src={setupData.qrCode}
                                    alt="MFA QR Code"
                                    className="border border-gray-700 rounded"
                                    onError={(e) => {
                                        console.error('[MfaSetupModal] QR Code image failed to load:', setupData.qrCode);
                                        setError('Failed to load QR code image');
                                    }}
                                    onLoad={() => {
                                    }}
                                />
                                <p className="text-xs text-gray-400 text-center">
                                    Can't scan? Enter this code manually:<br />
                                    <code className="bg-gray-800 px-2 py-1 rounded text-white">{setupData.manualEntryKey}</code>
                                </p>
                            </div>
                        ) : (
                            <div className="text-center text-gray-400 py-4">
                                No QR code data received
                            </div>
                        )}

                        <button
                            onClick={() => setStep(2)}
                            disabled={loading || !setupData}
                            className="w-full bg-yellow-600 text-black py-2 px-4 rounded hover:bg-yellow-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                        >
                            Continue
                        </button>
                    </div>
                )}

                {step === 2 && (
                    <div className="space-y-4">
                        <p className="text-sm text-gray-300">
                            Enter the 6-digit code from your authenticator app to complete setup:
                        </p>

                        <input
                            type="text"
                            placeholder="000000"
                            value={totpCode}
                            onChange={(e) => setTotpCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                            className="w-full px-3 py-2 bg-gray-800 border border-gray-700 rounded-md text-white focus:outline-none focus:ring-2 focus:ring-yellow-500 text-center text-lg tracking-widest"
                            maxLength={6}
                            autoComplete="off"
                            autoFocus
                            disabled={loading}
                        />

                        <div className="flex space-x-3">
                            <button
                                onClick={() => setStep(1)}
                                className="flex-1 bg-gray-700 hover:bg-gray-600 text-white py-2 px-4 rounded transition-colors"
                                disabled={loading}
                            >
                                Back
                            </button>
                            <button
                                onClick={verifySetup}
                                disabled={loading || totpCode.length !== 6}
                                className="flex-1 bg-yellow-600 hover:bg-yellow-700 text-black py-2 px-4 rounded disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
                            >
                                {loading ? 'Verifying...' : 'Verify'}
                            </button>
                        </div>
                    </div>
                )}

                {step === 3 && (
                    <div className="space-y-4">
                        <div className="bg-green-900 border border-green-800 text-green-300 px-4 py-3 rounded">
                            ✓ Two-factor authentication is now enabled!
                        </div>

                        <div>
                            <h3 className="font-medium text-white mb-2">Backup Codes</h3>
                            <p className="text-sm text-gray-300 mb-3">
                                Save these backup codes in a safe place. Each code can only be used once if you lose access to your authenticator.
                            </p>

                            <div className="bg-gray-800 border border-gray-700 rounded p-3 mb-3">
                                <div className="grid grid-cols-2 gap-2 text-sm font-mono text-white">
                                    {setupData?.backupCodes?.map((code, index) => (
                                        <div key={index} className="text-center">{code}</div>
                                    ))}
                                </div>
                            </div>

                            <button
                                onClick={downloadBackupCodes}
                                className="w-full bg-gray-600 hover:bg-gray-700 text-white py-2 px-4 rounded transition-colors mb-3"
                            >
                                Download Backup Codes
                            </button>
                        </div>

                        <button
                            onClick={finishSetup}
                            className="w-full bg-yellow-600 hover:bg-yellow-700 text-black py-2 px-4 rounded transition-colors"
                        >
                            Finish Setup
                        </button>
                    </div>
                )}
            </div>
        </div>
    );
};

export default MfaSetupModal;