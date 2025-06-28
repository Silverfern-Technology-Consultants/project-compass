// OAuthSuccessPage.jsx - Create this new file in components/pages/
import React, { useEffect } from 'react';
import { CheckCircle, X } from 'lucide-react';

const OAuthSuccessPage = () => {
    useEffect(() => {
        // Auto-close the popup window after a short delay
        const timer = setTimeout(() => {
            console.log('[OAuthSuccessPage] Auto-closing OAuth popup window');
            if (window.opener) {
                // This is a popup window, close it
                window.close();
            } else {
                // Fallback: redirect to main app
                window.location.href = '/app/company/clients';
            }
        }, 2000); // 2 second delay to show success message

        return () => clearTimeout(timer);
    }, []);

    const handleCloseManually = () => {
        console.log('[OAuthSuccessPage] Manual close requested');
        if (window.opener) {
            window.close();
        } else {
            window.location.href = '/app/company/clients';
        }
    };

    return (
        <div className="min-h-screen bg-gray-950 flex items-center justify-center p-4">
            <div className="bg-gray-800 rounded-lg shadow-xl p-8 max-w-md w-full text-center">
                <div className="w-16 h-16 bg-green-600 rounded-full flex items-center justify-center mx-auto mb-6">
                    <CheckCircle size={32} className="text-white" />
                </div>

                <h1 className="text-2xl font-bold text-white mb-4">OAuth Setup Complete!</h1>

                <p className="text-gray-300 mb-6">
                    Your Azure environment has been successfully connected using OAuth delegation.
                    This window will close automatically.
                </p>

                <div className="flex justify-center space-x-3">
                    <button
                        onClick={handleCloseManually}
                        className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded font-medium flex items-center space-x-2"
                    >
                        <X size={16} />
                        <span>Close Window</span>
                    </button>
                </div>

                <p className="text-xs text-gray-500 mt-4">
                    Window will auto-close in 2 seconds...
                </p>
            </div>
        </div>
    );
};

export default OAuthSuccessPage;