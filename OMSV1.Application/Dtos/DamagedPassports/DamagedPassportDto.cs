namespace OMSV1.Application.Dtos
{
    public class DamagedPassportDto
    {
        public int Id { get; set; }
        public string PassportNumber { get; set; }
        public DateTime Date { get; set; }
        public int DamagedTypeId { get; set; }
        public string DamagedTypeName { get; set; }  // Assuming this is populated from the DamagedType entity
        public string? Note { get; set; }
        public int OfficeId { get; set; }
        public string OfficeName { get; set; }  // Assuming this is populated from the Office entity
        public int GovernorateId { get; set; }
        public string GovernorateName { get; set; }  // Assuming this is populated from the Governorate entity
        public int ProfileId { get; set; }
        public string ProfileFullName { get; set; }  // Assuming this is populated from the Profile entity

        public DamagedPassportDto(string passportNumber, DateTime date, int damagedTypeId, string damagedTypeName,string note,
                                  int officeId, string officeName, int governorateId, string governorateName,
                                  int profileId, string profileFullName)
        {
            PassportNumber = passportNumber;
            Date = date;
            DamagedTypeId = damagedTypeId;
            DamagedTypeName = damagedTypeName;
            Note=note;
            OfficeId = officeId;
            OfficeName = officeName;
            GovernorateId = governorateId;
            GovernorateName = governorateName;
            ProfileId = profileId;
            ProfileFullName = profileFullName;
        }
    }
}
