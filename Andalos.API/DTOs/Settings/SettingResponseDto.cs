namespace Andalos.API.DTOs.Settings
{
    public class SettingResponseDto
    {
        public string SettingKey { get; set; } = string.Empty;
        public string? SettingValue { get; set; }
        public string SettingGroup { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DefaultValue { get; set; }
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
    }

    public class SettingGroupDto
    {
        public string GroupName { get; set; } = string.Empty;
        public string GroupDisplayName { get; set; } = string.Empty;
        public List<SettingResponseDto> Settings { get; set; } = new();
    }
    public class UpdateSettingsDto
    {
        public string Group { get; set; } = string.Empty;
        public Dictionary<string, string> Values { get; set; } = new();
    }
}