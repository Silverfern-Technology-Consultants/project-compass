import React from 'react';

const FilterControls = ({ 
    timeRange, 
    setTimeRange, 
    aggregation, 
    setAggregation, 
    sortBy, 
    setSortBy, 
    sortDirection, 
    setSortDirection, 
    onApplyFilters, 
    isLoading 
}) => {
    return (
        <div className="bg-gray-900 border border-gray-800 rounded p-4">
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
                <div>
                    <label className="block text-sm font-medium text-gray-300 mb-2">Time Range</label>
                    <select
                        value={timeRange}
                        onChange={(e) => setTimeRange(e.target.value)}
                        className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                    >
                        <option value="LastMonthToThisMonth">Last Month to This Month</option>
                        <option value="Last3Months">Last 3 Months</option>
                        <option value="Last6Months">Last 6 Months</option>
                        <option value="LastYearToThisYear">Last Year to This Year</option>
                    </select>
                </div>

                <div>
                    <label className="block text-sm font-medium text-gray-300 mb-2">Group By</label>
                    <select
                        value={aggregation}
                        onChange={(e) => setAggregation(e.target.value)}
                        className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                    >
                        <option value="ResourceType">Resource Type</option>
                        <option value="ResourceGroup">Resource Group</option>
                        <option value="Subscription">Subscription</option>
                        <option value="Daily">Daily Costs</option>
                        <option value="None">Individual Resources</option>
                    </select>
                </div>

                <div>
                    <label className="block text-sm font-medium text-gray-300 mb-2">Sort By</label>
                    <select
                        value={sortBy}
                        onChange={(e) => setSortBy(e.target.value)}
                        className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                    >
                        <option value="Name">Name</option>
                        <option value="ResourceType">Resource Type</option>
                        <option value="PreviousPeriodCost">Previous Period Cost</option>
                        <option value="CurrentPeriodCost">Current Period Cost</option>
                        <option value="CostDifference">Cost Difference</option>
                        <option value="PercentageChange">Percentage Change</option>
                    </select>
                </div>

                <div>
                    <label className="block text-sm font-medium text-gray-300 mb-2">Order</label>
                    <select
                        value={sortDirection}
                        onChange={(e) => setSortDirection(e.target.value)}
                        className="w-full px-3 py-2 bg-gray-700 border border-gray-600 rounded text-white focus:outline-none focus:ring-2 focus:ring-yellow-600"
                    >
                        <option value="Ascending">Ascending</option>
                        <option value="Descending">Descending</option>
                    </select>
                </div>
            </div>

            <div className="mt-4 flex justify-end">
                <button
                    onClick={onApplyFilters}
                    disabled={isLoading}
                    className="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-black rounded text-sm font-medium disabled:opacity-50"
                >
                    Apply Filters
                </button>
            </div>
        </div>
    );
};

export default FilterControls;