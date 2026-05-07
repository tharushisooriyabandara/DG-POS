using System;
using System.Linq;

namespace POS_UI.Models
{
    public class CurrentUserModel
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Address { get; set; }
        public string ContactNo { get; set; }
        public string Status { get; set; }
        public string RoleId { get; set; }
        public string Role { get; set; }
        public string ReportServiceToken { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        
        public string FullName => string.IsNullOrEmpty(LastName) ? FirstName : $"{FirstName} {LastName}";
        public string Initials => string.Join("", (FirstName + " " + LastName).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(n => n[0])).ToUpper();
    }
} 