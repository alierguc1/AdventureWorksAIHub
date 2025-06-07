using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Application.Dtos.Products
{
    public class SearchResultDto
    {
        public int ProductID { get; set; }
        public string Text { get; set; }
        public float Similarity { get; set; }
    }
}
