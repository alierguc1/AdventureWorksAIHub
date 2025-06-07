using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Domain.Entities.Product
{
    [Table("ProductDescription", Schema = "Production")]
    public class ProductDescription
    {
        [Key]
        public int ProductDescriptionID { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(400)")]
        public string Description { get; set; } = string.Empty;

        public Guid rowguid { get; set; }

        public DateTime ModifiedDate { get; set; }
    }
}
