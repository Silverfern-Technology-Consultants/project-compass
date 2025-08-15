import React from 'react';

const ComplianceTab = ({ formData, setFormData, handleCheckboxChange }) => {
    const availableCompliances = [
        { name: "SOC 2", description: "Service Organization Control 2" },
        { name: "HIPAA", description: "Health Insurance Portability and Accountability Act" },
        { name: "PCI DSS", description: "Payment Card Industry Data Security Standard" },
        { name: "ISO 27001", description: "Information Security Management Systems" },
        { name: "GDPR", description: "General Data Protection Regulation" },
        { name: "FedRAMP", description: "Federal Risk and Authorization Management Program" },
        { name: "NIST", description: "National Institute of Standards and Technology" },
        { name: "CIS Controls", description: "Center for Internet Security Controls" },
        { name: "FISMA", description: "Federal Information Security Management Act" },
        { name: "SOX", description: "Sarbanes-Oxley Act" }
    ];

    return (
        <div className="space-y-6">
            {/* No Specific Requirements Option */}
            <div>
                <label className="flex items-center space-x-3 p-4 bg-gray-700 rounded-lg cursor-pointer hover:bg-gray-600">
                    <input
                        type="checkbox"
                        checked={formData.selectedCompliances.length === 0}
                        onChange={(e) => {
                            if (e.target.checked) {
                                setFormData(prev => ({ ...prev, selectedCompliances: [] }));
                            }
                        }}
                        className="text-yellow-600 focus:ring-yellow-600"
                    />
                    <div>
                        <div className="text-white font-medium">No Specific Requirements</div>
                        <div className="text-gray-300 text-sm">Standard governance only - no compliance frameworks required</div>
                    </div>
                </label>
            </div>

            {/* Compliance Frameworks - Always Visible */}
            <div>
                <h3 className="text-white font-medium mb-4">Applicable Frameworks</h3>
                <p className="text-gray-400 text-sm mb-4">
                    Select the compliance frameworks that apply to this client's environment.
                </p>

                {/* Quick Selection - Common Frameworks */}
                <div className="mb-6">
                    <h4 className="text-gray-300 font-medium mb-3">Common Frameworks</h4>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                        {[
                            { name: "PCI DSS", description: "Payment Card Industry Data Security Standard" },
                            { name: "SOC 2", description: "Service Organization Control 2" },
                            { name: "CIS Controls", description: "Center for Internet Security Controls" }
                        ].map(compliance => (
                            <label key={compliance.name} className="flex items-start space-x-3 p-4 bg-blue-900/20 border border-blue-800 rounded-lg cursor-pointer hover:bg-blue-900/30">
                                <input
                                    type="checkbox"
                                    checked={formData.selectedCompliances.includes(compliance.name)}
                                    onChange={() => handleCheckboxChange('selectedCompliances', compliance.name)}
                                    className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                />
                                <div className="flex-1">
                                    <div className="text-blue-300 text-sm font-medium">{compliance.name}</div>
                                    <div className="text-blue-400 text-xs mt-1">{compliance.description}</div>
                                </div>
                            </label>
                        ))}
                    </div>
                </div>

                {/* All Available Frameworks */}
                <div>
                    <h4 className="text-gray-300 font-medium mb-3">All Available Frameworks</h4>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                        {availableCompliances.map(compliance => (
                            <label key={compliance.name} className="flex items-start space-x-3 p-3 bg-gray-700 rounded cursor-pointer hover:bg-gray-600">
                                <input
                                    type="checkbox"
                                    checked={formData.selectedCompliances.includes(compliance.name)}
                                    onChange={() => handleCheckboxChange('selectedCompliances', compliance.name)}
                                    className="mt-1 text-yellow-600 focus:ring-yellow-600"
                                />
                                <div className="flex-1">
                                    <div className="text-gray-300 text-sm font-medium">{compliance.name}</div>
                                    <div className="text-gray-400 text-xs mt-1">{compliance.description}</div>
                                </div>
                            </label>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default ComplianceTab;