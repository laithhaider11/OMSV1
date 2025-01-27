using System;
using OMSV1.Domain.Entities.DamagedDevices;

namespace OMSV1.Domain.Specifications.DamagedDevices;

public class FilterDamagedDevicesSpecification : BaseSpecification<DamagedDevice>
{
    public FilterDamagedDevicesSpecification(
        string? serialNumber = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        Guid? damagedDeviceTypeId = null,
        Guid? deviceTypeId = null,
        Guid? officeId = null,
        Guid? governorateId = null,
        Guid? profileId = null)
        : base(x =>
            (string.IsNullOrEmpty(serialNumber) || x.SerialNumber.Contains(serialNumber)) &&
            (!startDate.HasValue || x.Date >= startDate.Value) &&
            (!endDate.HasValue || x.Date <= endDate.Value) &&
            (!damagedDeviceTypeId.HasValue || x.DamagedDeviceTypeId == damagedDeviceTypeId.Value) &&
            (!deviceTypeId.HasValue || x.DeviceTypeId == deviceTypeId.Value) &&
            (!officeId.HasValue || x.OfficeId == officeId.Value) &&
            (!governorateId.HasValue || x.GovernorateId == governorateId.Value) &&
            (!profileId.HasValue || x.ProfileId == profileId.Value))
    {
        AddInclude(x => x.Governorate);
        AddInclude(x => x.DeviceType);
        AddInclude(x => x.DamagedDeviceTypes);
        AddInclude(x => x.Office);
        AddInclude(x => x.Profile);
                   // Apply ordering
         ApplyOrderByDescending(x => x.DateCreated);
    }
}
