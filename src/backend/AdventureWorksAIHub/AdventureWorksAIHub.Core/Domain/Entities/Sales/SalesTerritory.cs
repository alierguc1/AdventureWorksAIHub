using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Domain.Entities.Sales
{
    [Table("SalesTerritory", Schema = "Sales")]
    public class SalesTerritory
    {
        [Key]
        public int TerritoryID { get; set; }
        public string Name { get; set; }
        public string CountryRegionCode { get; set; }
        public string Group { get; set; }
        public decimal SalesYTD { get; set; }
        public decimal SalesLastYear { get; set; }
        public decimal CostYTD { get; set; }
        public decimal CostLastYear { get; set; }
        public Guid rowguid { get; set; }
        public DateTime ModifiedDate { get; set; }

        // Navigation Properties
        public virtual ICollection<SalesOrderHeader> SalesOrderHeaders { get; set; }
    }
}
