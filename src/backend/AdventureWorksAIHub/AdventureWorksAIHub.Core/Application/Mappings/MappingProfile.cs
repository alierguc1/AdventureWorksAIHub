using AdventureWorksAIHub.Core.Application.Dtos.Product;
using AdventureWorksAIHub.Core.Domain.Entities.Product;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdventureWorksAIHub.Core.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Product -> ProductDto mapping
            CreateMap<Product, ProductDto>()
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src =>
                    src.ProductDescription != null ? src.ProductDescription.Description : null));

            // ProductInfoDto için eşleştirme (varsa)
            CreateMap<Product, ProductInfoDto>()
                .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.ListPrice));
        }
    }
}
