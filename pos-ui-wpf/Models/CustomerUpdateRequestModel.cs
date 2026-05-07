using System.Collections.Generic;

namespace POS_UI.Models
{
    public class CustomerUpdateRequestModel
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string CountryCode { get; set; }
        public string Phone { get; set; }
        public List<CustomerAddressModel> Addresses { get; set; } = new List<CustomerAddressModel>();
    }
}
