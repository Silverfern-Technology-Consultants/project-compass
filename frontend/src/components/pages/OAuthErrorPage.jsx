// OAuthErrorPage.jsx - Create this new file in components/pages/
import React, { useEffect, useState } from 'react';
import { AlertCircle, X, RefreshCw } from 'lucide-react';

const OAuthErrorPage = () => {
    const [errorMessage, setErrorMessage] = useState('OAuth authentication failed');

    useEffect(() => {
        // Extract error message from URL parameters
        const urlParams = new URLSearchParams(window.location.search);
        const error = urlParams.get('message') || 'OAuth authentication failed';
        setErrorMessage(decodeURIComponent(error));

        console.log('[OAuthErrorPage] OAuth error:', error);
    }, []);

    const handleCloseWindow = () => {
        console.log('[OAuthErrorPage] Closing OAuth error window');
        if (window.opener) {
            window.close();
        } else {
            window.location.href = '/app/company/clients';
        }
    };

    const handleRetry = () => {
        console.log('[OAuthErrorPage] Retrying OAuth flow');
        if (window.opener) {
            // Close this window and let the parent handle retry
            window.close();
        } else {
            window.location.href = '/app/company/clients';
        }
    };

    return (
        <div className="min-h-screen bg-gray-950 flex items-center justify-center p-4">
            <div className="bg-gray-800 rounded-lg shadow-xl p-8 max-w-md w-full text-center">
                <div className="w-16 h-16 bg-red-600 rounded-full flex items-center justify-center mx-auto mb-6">
                    <AlertCircle size={32} className="text-white" />
                </div>

                <h1 className="text-2xl font-bold text-white mb-4">OAuth Setup Failed</h1>

                <p className="text-gray-300 mb-2">
                    There was an issue setting up OAuth for your Azure environment:
                </p>

                <div className="bg-red-900/20 border border-red-800 rounded p-3 mb-6">
                    <p className="text-red-300 text-sm">
                        {errorMessage}
                    </p>
                </div>

                <div className="space-y-3">
                    <p className="text-xs text-gray-400">
                        Common solutions:
                    </p>
                    <ul className="text-xs text-gray-400 text-left space-y-1">
                        <li>• Ensure you have admin access to the Azure tenant</li>
                        <li>• Check that popup blockers are disabled</li>
                        <li>• Verify the tenant ID is correct</li>
                        <li>• Try again or use manual Service Principal setup</li>
                    </ul>
                </div>

                <div className="flex justify-center space-x-3 mt-6">
                    <button
                        onClick={handleRetry}
                        className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2"
                    >
                        <RefreshCw size={16} />
                        <span>Try Again</span>
                    </button>

                    <button
                        onClick={handleCloseWindow}
                        className="px-4 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded font-medium flex items-center space-x-2"
                    >
                        <X size={16} />
                        <span>Close</span>
                    </button>
                </div>
            </div>
        </div>
    );
};

export default OAuthErrorPage;