using AutoMapper;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForumService.Core.Extensions
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Map 2 chiều giữa Entity và DTO
            CreateMap<Post, PostDto>().ReverseMap();
        }
    }
}
