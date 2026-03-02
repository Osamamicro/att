using System.Text.Json.Serialization;

namespace Application.DTOs.Nifi;

public sealed class NifiAttendanceRecordDto
{
    [JsonPropertyName("person_number")]
    public string PersonNumber { get; set; } = string.Empty;

    [JsonPropertyName("full_name_ar")]
    public string FullNameAr { get; set; } = string.Empty;

    [JsonPropertyName("position_ar_name")]
    public string PositionArName { get; set; } = string.Empty;

    [JsonPropertyName("sector_ar")]
    public string SectorArName { get; set; } = string.Empty;

    [JsonPropertyName("gdept_ar")]
    public string GeneralDepartmentArName { get; set; } = string.Empty;

    [JsonPropertyName("dept_ar")]
    public string DepartmentArName { get; set; } = string.Empty;

    [JsonPropertyName("section_ar")]
    public string SectionArName { get; set; } = string.Empty;

    [JsonPropertyName("location_ar_name")]
    public string LocationNameAr { get; set; } = string.Empty;

    [JsonPropertyName("isdepartmentmanager")]
    public string IsDepartmentManager { get; set; } = string.Empty;

    [JsonPropertyName("shift_name")]
    public string ShiftName { get; set; } = string.Empty;

    [JsonPropertyName("work_date")]
    public string WorkDate { get; set; } = string.Empty;

    [JsonPropertyName("start_date_time")]
    public string StartDateTime { get; set; } = string.Empty;

    [JsonPropertyName("end_date_time")]
    public string EndDateTime { get; set; } = string.Empty;

    [JsonPropertyName("First in")]
    public string FirstIn { get; set; } = string.Empty;

    [JsonPropertyName("Last Out")]
    public string LastOut { get; set; } = string.Empty;

    [JsonPropertyName("First In Gate")]
    public string FirstInGate { get; set; } = string.Empty;

    [JsonPropertyName("Last Out Gate")]
    public string LastOutGate { get; set; } = string.Empty;

    [JsonPropertyName("late_hhmm")]
    public string Late { get; set; } = string.Empty;

    [JsonPropertyName("early_hhmm")]
    public string Early { get; set; } = string.Empty;

    [JsonPropertyName("total_missed_hhmm")]
    public string TotalMissed { get; set; } = string.Empty;

    [JsonPropertyName("inside_hhmm")]
    public string Inside { get; set; } = string.Empty;

    [JsonPropertyName("Required Hours (Day)")]
    public string RequiredHoursDay { get; set; } = string.Empty;

    [JsonPropertyName("Actual Hours (Day)")]
    public string ActualHoursDay { get; set; } = string.Empty;

    [JsonPropertyName("Balance (Day)")]
    public string BalanceActualRequired { get; set; } = string.Empty;

    [JsonPropertyName("attendance_status")]
    public string AttendanceStatus { get; set; } = string.Empty;

    [JsonPropertyName("absence_type")]
    public string? AbsenceType { get; set; }

    [JsonPropertyName("attendance completeness")]
    public string AttendanceCompleteness { get; set; } = string.Empty;
}
