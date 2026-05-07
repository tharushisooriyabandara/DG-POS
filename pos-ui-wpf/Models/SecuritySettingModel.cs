using System;

namespace POS_UI.Models
{
    public class SecuritySettingModel
    {
        public string UserName { get; set; }
        public string Status { get; set; }
        public DateTime LastActive { get; set; }
        public bool IsActive { get; set; }
        public bool PinChangeRequested { get; set; }
    }
} 