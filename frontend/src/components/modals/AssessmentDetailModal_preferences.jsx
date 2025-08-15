="text-blue-400" />
                                                        <h4 className="font-medium text-blue-400 text-sm">Want Customized Standards?</h4>
                                                    </div>
                                                    <p className="text-blue-300 text-sm">
                                                        Configure client-specific preferences to enhance assessments with custom naming patterns, 
                                                        required tags, and compliance frameworks tailored to this client's needs.
                                                    </p>
                                                </div>
                                            </div>
                                        )}

                                        {/* Client preferences applied */}
                                        {clientPreferences !== null && Object.keys(clientPreferences).length > 0 && (
                                            <div className="flex-1 space-y-4 overflow-y-auto">
                                                {/* Client Custom Preferences Header */}
                                                <div className="bg-blue-600/10 border border-blue-600/30 rounded-lg p-4">
                                                    <div className="flex items-center space-x-2 mb-3">
                                                        <User size={16} className="text-blue-400" />
                                                        <h4 className="font-medium text-blue-400">Client Custom Preferences</h4>
                                                        <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-blue-600 text-white">
                                                            Active
                                                        </span>
                                                    </div>
                                                    <div className="space-y-2 text-sm">
                                                        <div className="flex items-start space-x-2">
                                                            <CheckCircle size={14} className="text-blue-400 mt-0.5 flex-shrink-0" />
                                                            <span className="text-blue-300">Custom naming patterns configured by MSP for this client</span>
                                                        </div>
                                                        <div className="flex items-start space-x-2">
                                                            <CheckCircle size={14} className="text-blue-400 mt-0.5 flex-shrink-0" />
                                                            <span className="text-blue-300">Client-specific required tags and validation rules</span>
                                                        </div>
                                                        <div className="flex items-start space-x-2">
                                                            <CheckCircle size={14} className="text-blue-400 mt-0.5 flex-shrink-0" />
                                                            <span className="text-blue-300">Enhanced compliance frameworks tailored to client needs</span>
                                                        </div>
                                                        <div className="flex items-start space-x-2">
                                                            <CheckCircle size={14} className="text-blue-400 mt-0.5 flex-shrink-0" />
                                                            <span className="text-blue-300">Stricter governance standards than Governance Guardian defaults</span>
                                                        </div>
                                                    </div>
                                                </div>

                                                {/* Custom Findings Count */}
                                                {findings.filter(f => f.isClientSpecific || f.IsClientSpecific).length > 0 && (
                                                    <div className="bg-orange-600/10 border border-orange-600/30 rounded-lg p-4">
                                                        <div className="flex items-center space-x-2 mb-2">
                                                            <AlertTriangle size={16} className="text-orange-400" />
                                                            <h4 className="font-medium text-orange-400 text-sm">Client-Specific Issues Found</h4>
                                                        </div>
                                                        <p className="text-orange-300 text-sm">
                                                            {findings.filter(f => f.isClientSpecific || f.IsClientSpecific).length} issues were identified using your custom client standards. 
                                                            These may not appear in standard assessments but are important for this client's specific requirements.
                                                        </p>
                                                    </div>
                                                )}

                                                {/* Naming Convention Details */}
                                                <div className="bg-gray-700/50 rounded-lg p-4">
                                                    <h4 className="font-medium text-gray-300 mb-3 flex items-center space-x-2">
                                                        <Tag size={16} className="text-gray-400" />
                                                        <span>NAMING CONVENTION</span>
                                                    </h4>
                                                    {clientPreferences.namingSchemeConfiguration || clientPreferences.NamingSchemeConfiguration ? (
                                                        <div className="space-y-3">
                                                            <div className="flex items-center space-x-2">
                                                                <span className="text-gray-300 text-sm font-medium">Pattern:</span>
                                                                <span className="text-yellow-400 text-sm">{clientPreferences.namingStyle || clientPreferences.NamingStyle || 'standardized'}</span>
                                                            </div>
                                                            
                                                            {(() => {
                                                                try {
                                                                    const config = typeof (clientPreferences.namingSchemeConfiguration || clientPreferences.NamingSchemeConfiguration) === 'string' 
                                                                        ? JSON.parse(clientPreferences.namingSchemeConfiguration || clientPreferences.NamingSchemeConfiguration)
                                                                        : (clientPreferences.namingSchemeConfiguration || clientPreferences.NamingSchemeConfiguration);
                                                                    
                                                                    const components = config?.Components || [];
                                                                    const separator = config?.Separator || '-';
                                                                    const caseFormat = config?.CaseFormat || 'lowercase';
                                                                    
                                                                    return (
                                                                        <div>
                                                                            <p className="text-gray-300 text-sm mb-2">Components ({components.length}):</p>
                                                                            <div className="bg-gray-800 rounded px-3 py-2 space-y-1">
                                                                                {components.map((component, index) => (
                                                                                    <div key={index} className="flex items-center space-x-2 text-xs">
                                                                                        <span className="text-green-400 font-mono">
                                                                                            {component.ComponentType}
                                                                                        </span>
                                                                                        {component.IsRequired && (
                                                                                            <span className="px-1 rounded text-xs bg-red-600 text-white">required</span>
                                                                                        )}
                                                                                        {component.Format && (
                                                                                            <span className="text-gray-400">({component.Format})</span>
                                                                                        )}
                                                                                    </div>
                                                                                ))}
                                                                                <div className="pt-1 border-t border-gray-700 mt-2">
                                                                                    <span className="text-gray-400 text-xs">
                                                                                        Separator: <span className="text-yellow-400">"{separator}"</span> | 
                                                                                        Case: <span className="text-yellow-400">{caseFormat}</span>
                                                                                    </span>
                                                                                </div>
                                                                            </div>
                                                                        </div>
                                                                    );
                                                                } catch (e) {
                                                                    return (
                                                                        <div className="bg-gray-800 rounded px-3 py-2 font-mono text-xs text-green-400">
                                                                            Custom naming scheme configured
                                                                        </div>
                                                                    );
                                                                }
                                                            })()}
                                                        </div>
                                                    ) : (
                                                        <div className="space-y-2">
                                                            <p className="text-gray-300 text-sm">Using enhanced naming validation</p>
                                                            <div className="bg-gray-800 rounded px-3 py-2 font-mono text-xs text-gray-400">
                                                                Standard patterns with stricter validation
                                                            </div>
                                                        </div>
                                                    )}
                                                </div>

                                                {/* Required Tags */}
                                                <div className="bg-gray-700/50 rounded-lg p-4">
                                                    <h4 className="font-medium text-gray-300 mb-3 flex items-center space-x-2">
                                                        <Tag size={16} className="text-gray-400" />
                                                        <span>REQUIRED TAGS</span>
                                                    </h4>
                                                    
                                                    {/* Selected Tags */}
                                                    {(clientPreferences.selectedTags || clientPreferences.SelectedTags) && (
                                                        <div className="space-y-2">
                                                            <p className="text-gray-300 text-sm">Standard Required Tags:</p>
                                                            <div className="flex flex-wrap gap-2">
                                                                {(clientPreferences.selectedTags || clientPreferences.SelectedTags).map(tag => (
                                                                    <span key={tag} className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-blue-600/20 text-blue-300 border border-blue-600/30">
                                                                        {tag}
                                                                    </span>
                                                                ))}
                                                            </div>
                                                        </div>
                                                    )}

                                                    {/* Custom Tags */}
                                                    {(clientPreferences.customTags || clientPreferences.CustomTags) && (
                                                        <div className="space-y-2 mt-3">
                                                            <p className="text-gray-300 text-sm">Client-Specific Tags:</p>
                                                            <div className="flex flex-wrap gap-2">
                                                                {(clientPreferences.customTags || clientPreferences.CustomTags).map(tag => (
                                                                    <span key={tag} className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-purple-600/20 text-purple-300 border border-purple-600/30">
                                                                        {tag}
                                                                    </span>
                                                                ))}
                                                            </div>
                                                        </div>
                                                    )}

                                                    <div className="flex items-center space-x-2 mt-3 pt-2 border-t border-gray-600">
                                                        <span className="text-gray-400 text-xs">Tagging Approach:</span>
                                                        <span className="text-yellow-400 text-xs font-medium capitalize">
                                                            {clientPreferences.taggingApproach || clientPreferences.TaggingApproach || 'comprehensive'}
                                                        </span>
                                                        {(clientPreferences.enforceTagCompliance || clientPreferences.EnforceTagCompliance) && (
                                                            <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-red-600/20 text-red-300">
                                                                Strict Enforcement
                                                            </span>
                                                        )}
                                                    </div>
                                                </div>

                                                {/* Company Names */}
                                                {(clientPreferences.acceptedCompanyNames || clientPreferences.AcceptedCompanyNames) && (
                                                    <div className="bg-gray-700/50 rounded-lg p-4">
                                                        <h4 className="font-medium text-gray-300 mb-3 flex items-center space-x-2">
                                                            <Globe size={16} className="text-gray-400" />
                                                            <span>ACCEPTED COMPANY NAMES</span>
                                                        </h4>
                                                        <div className="flex flex-wrap gap-2">
                                                            {(clientPreferences.acceptedCompanyNames || clientPreferences.AcceptedCompanyNames).map(name => (
                                                                <span key={name} className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-green-600/20 text-green-300 border border-green-600/30">
                                                                    {name}
                                                                </span>
                                                            ))}
                                                        </div>
                                                    </div>
                                                )}

                                                {/* Compliance Framework */}
                                                <div className="bg-gray-700/50 rounded-lg p-4">
                                                    <h4 className="font-medium text-gray-300 mb-3 flex items-center space-x-2">
                                                        <Shield size={16} className="text-gray-400" />
                                                        <span>COMPLIANCE FRAMEWORK</span>
                                                    </h4>
                                                    {(clientPreferences.selectedComplainces || clientPreferences.SelectedComplainces) ? (
                                                        <div className="space-y-2">
                                                            <p className="text-gray-300 text-sm">Client-selected compliance frameworks:</p>
                                                            <div className="flex flex-wrap gap-2">
                                                                {(clientPreferences.selectedComplainces || clientPreferences.SelectedComplainces).map(framework => (
                                                                    <span key={framework} className="inline-flex items-center px-2 py-1 rounded-full text-xs font-medium bg-purple-600/20 text-purple-300 border border-purple-600/30">
                                                                        {framework}
                                                                    </span>
                                                                ))}
                                                            </div>
                                                        </div>
                                                    ) : (
                                                        <p className="text-gray-300 text-sm">Enhanced compliance validation using industry standards</p>
                                                    )}
                                                </div>

                                                {/* Environment Configuration */}
                                                <div className="bg-gray-700/50 rounded-lg p-4">
                                                    <h4 className="font-medium text-gray-300 mb-3 flex items-center space-x-2">
                                                        <Server size={16} className="text-gray-400" />
                                                        <span>ENVIRONMENT CONFIGURATION</span>
                                                    </h4>
                                                    <div className="grid grid-cols-2 gap-3 text-sm">
                                                        <div>
                                                            <span className="text-gray-400">Indicators:</span>
                                                            <p className="text-white">
                                                                {clientPreferences.environmentIndicatorLevel || clientPreferences.EnvironmentIndicatorLevel || 'Standard'}
                                                            </p>
                                                        </div>
                                                        <div>
                                                            <span className="text-gray-400">Size:</span>
                                                            <p className="text-white capitalize">
                                                                {clientPreferences.environmentSize || clientPreferences.EnvironmentSize || 'medium'}
                                                            </p>
                                                        </div>
                                                        <div>
                                                            <span className="text-gray-400">Organization:</span>
                                                            <p className="text-white capitalize">
                                                                {clientPreferences.organizationMethod || clientPreferences.OrganizationMethod || 'environment'}
                                                            </p>
                                                        </div>
                                                        <div>
                                                            <span className="text-gray-400">Modified:</span>
                                                            <p className="text-white">
                                                                {clientPreferences.lastModifiedDate || clientPreferences.LastModifiedDate ? 
                                                                    new Date(clientPreferences.lastModifiedDate || clientPreferences.LastModifiedDate).toLocaleDateString() : 
                                                                    'Unknown'
                                                                }
                                                            </p>
                                                        </div>
                                                    </div>
                                                </div>

                                                {/* Assessment Impact */}
                                                <div className="bg-green-600/10 border border-green-600/30 rounded-lg p-4">
                                                    <div className="flex items-center space-x-2 mb-2">
                                                        <Target size={16} className="text-green-400" />
                                                        <h4 className="font-medium text-green-400 text-sm">Assessment Impact</h4>
                                                    </div>
                                                    <p className="text-green-300 text-sm">
                                                        This assessment provides enhanced recommendations and findings specifically tailored to {assessment.clientName || 'this client'}'s 
                                                        governance requirements, resulting in more precise and actionable insights than standard assessments.
                                                    </p>
                                                </div>
                                            </div>
                                        )}
                                    </div>