using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Domain.Entities.Sales
{
    [Table("Customer", Schema = "Sales")]
    public class Customer
    {
        [Key]
        public int CustomerID { get; set; }
        public int? PersonID { get; set; }
        public int? StoreID { get; set; }
        public int? TerritoryID { get; set; }
        public string AccountNumber { get; set; }
        public Guid rowguid { get; set; }
        public DateTime ModifiedDate { get; set; }

        // Navigation Properties
        public virtual ICollection<SalesOrderHeader> SalesOrderHeaders { get; set; }
    }
}
