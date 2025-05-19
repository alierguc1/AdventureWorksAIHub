using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Application.Dtos
{
    public class RagResponseDto
    {
        public string Answer { get; set; }
        public List<ProductInfoDto> RelatedProducts { get; set; } = new List<ProductInfoDto>();
    }
}
