using AutoMapper;
using ForumService.Contract.TransferObjects.Category;
using ForumService.Contract.TransferObjects.Post;
using ForumService.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ForumService.Contract.UseCases.Category.Command;
using static ForumService.Contract.UseCases.Category.Request;
using static ForumService.Contract.UseCases.Post.Command;
using static ForumService.Contract.UseCases.Post.Request;

namespace ForumService.Core.Extensions
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Post, PostViewDto>().ReverseMap();

            // Mapping Category
            CreateMap<Category, CategoryDto>().ReverseMap();
            CreateMap<CreateCategoryRequest, CreateCategoryCommand>();
            CreateMap<UpdateCategoryRequest, UpdateCategoryCommand>();


          
        }
    }
}
