using System;
using OMSV1.Domain.Entities.Expenses;

namespace OMSV1.Domain.Specifications.Expenses
{
    public class FilterLastMonthExpensesSpecification : BaseSpecification<MonthlyExpenses>
    {
        public FilterLastMonthExpensesSpecification(
            Guid? officeId = null,
            Guid? governorateId = null,
            Guid? thresholdId = null, // Filter by ThresholdId
            DateTime? startDate = null,
            DateTime? endDate = null)
            : base(x =>
                (!officeId.HasValue || x.OfficeId == officeId) &&
                (!governorateId.HasValue || x.GovernorateId == governorateId) &&
                (!thresholdId.HasValue || x.ThresholdId == thresholdId) && // Threshold filter
                x.DateCreated >= startDate.Value.AddMonths(-1).Date && // Start of last month
                x.DateCreated < endDate.Value.AddMonths(-1).Date.AddDays(1)) // End of last month
        {
            // Include related entities
            AddInclude(x => x.Office);
            AddInclude(x => x.Governorate);
            AddInclude(x => x.Threshold); // Include Threshold details

            // Apply ordering by Threshold.MaxValue (primary sort)
            ApplyOrderByDescending(x => x.TotalAmount);
        }
    }
}
