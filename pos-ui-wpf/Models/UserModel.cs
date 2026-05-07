using System;
using System.Linq;

namespace POS_UI.Models
{
    public class UserModel
    {
        public Guid ID { get; set; } = Guid.NewGuid();
        public int ApiId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Role { get; set; } // e.g., "Admin", "Cashier"
        public string Pin { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string RoleId { get; set; }
        public string FullName => string.IsNullOrEmpty(LastName) ? FirstName : $"{FirstName} {LastName}";
        public string Initials
        {
            get
            {
                var names = (FirstName + " " + LastName) ?? string.Empty;
                var letters = string.Join("", names
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(n => n[0]))
                    .ToUpper();
                return letters.Length > 2 ? letters.Substring(0, 2) : letters;
            }
        }
    }
} 