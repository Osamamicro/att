using Application.DTOs.Nifi;

namespace Application.Service.Nifi;

public interface INifiService
{
    Task<List<NifiAttendanceRecordDto>> GetAttendanceRecordsAsync(string personNumber, DateTime startDate, DateTime endDate);
    Task<List<NifiAttendanceRecordDto>> GetSectionAttendanceRecordsAsync(string section, DateTime startDate, DateTime endDate, string personNumber = "");
}
