using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Domain.Entities
{
    public class ProductVector
    {
        public int ProductVectorID { get; set; }
        public int ProductID { get; set; }
        public string Text { get; set; }
        public string Embedding { get; set; }
        public virtual Product Product { get; set; }
    }
}
