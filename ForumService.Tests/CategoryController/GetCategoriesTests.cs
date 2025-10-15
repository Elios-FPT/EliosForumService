using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Category;
using ForumService.Web.Controllers.Category;
using MediatR;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Category.Query;

namespace ForumService.Tests.Controllers.CategoryController
{
    public class GetCategoriesTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Category.CategoryController _controller;
        private readonly Guid _validCategoryId = Guid.Parse("8cf071b9-ea2e-4a19-865e-28ec04a26ba7");

        public GetCategoriesTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Category.CategoryController(_senderMock.Object);
        }

        [Fact]
        public async Task GetCategories_ReturnsSuccessWithMultipleCategories()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 20, 0);
            var categories = new List<CategoryDto>
            {
                new CategoryDto { CategoryId = _validCategoryId, Name = "Category 1", Slug = "category-1", Description = "Description 1", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new CategoryDto { CategoryId = Guid.NewGuid(), Name = "Category 2", Slug = "category-2", Description = "Description 2", IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };
            var expectedResponse = new BaseResponseDto<IEnumerable<CategoryDto>>
            {
                Status = 200,
                Message = "Categories retrieved successfully.",
                ResponseData = categories
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryListQuery>(q => q.SearchKeyword == null && q.IsActive == null && q.Limit == 20 && q.Offset == 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Equal(2, result.ResponseData.Count());
            Assert.Equal("Categories retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategories_ReturnsSuccessWithSingleCategory()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 20, 0);
            var categories = new List<CategoryDto>
            {
                new CategoryDto { CategoryId = _validCategoryId, Name = "Category 1", Slug = "category-1", Description = "Description 1", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };
            var expectedResponse = new BaseResponseDto<IEnumerable<CategoryDto>>
            {
                Status = 200,
                Message = "Categories retrieved successfully.",
                ResponseData = categories
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryListQuery>(q => q.SearchKeyword == null && q.IsActive == null && q.Limit == 20 && q.Offset == 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Single(result.ResponseData);
            Assert.Equal("Category 1", result.ResponseData.First().Name);
            Assert.Equal("Categories retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategories_ReturnsSuccessWithEmptyList()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 20, 0);
            var expectedResponse = new BaseResponseDto<IEnumerable<CategoryDto>>
            {
                Status = 200,
                Message = "No categories found.",
                ResponseData = new List<CategoryDto>()
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryListQuery>(q => q.SearchKeyword == null && q.IsActive == null && q.Limit == 20 && q.Offset == 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Empty(result.ResponseData);
            Assert.Equal("No categories found.", result.Message);
        }

        [Fact]
        public async Task GetCategories_ThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 20, 0);
            _senderMock.Setup(s => s.Send(It.IsAny<GetCategoryListQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.Status);
            Assert.Null(result.ResponseData);
            Assert.StartsWith("Failed to retrieve categories: Database error", result.Message);
        }

        [Fact]
        public async Task GetCategories_FilterBySearchKeyword_ReturnsMatchingCategories()
        {
            // Arrange
            var query = new GetCategoryListQuery("tech", null, 20, 0);
            var categories = new List<CategoryDto>
            {
                new CategoryDto { CategoryId = _validCategoryId, Name = "Technology", Slug = "technology", Description = "Tech category", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };
            var expectedResponse = new BaseResponseDto<IEnumerable<CategoryDto>>
            {
                Status = 200,
                Message = "Categories retrieved successfully.",
                ResponseData = categories
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryListQuery>(q => q.SearchKeyword == "tech" && q.IsActive == null && q.Limit == 20 && q.Offset == 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Single(result.ResponseData);
            Assert.Equal("Technology", result.ResponseData.First().Name);
            Assert.Equal("Categories retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategories_FilterByIsActiveTrue_ReturnsActiveCategories()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, true, 20, 0);
            var categories = new List<CategoryDto>
            {
                new CategoryDto { CategoryId = _validCategoryId, Name = "Category 1", Slug = "category-1", Description = "Description 1", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };
            var expectedResponse = new BaseResponseDto<IEnumerable<CategoryDto>>
            {
                Status = 200,
                Message = "Categories retrieved successfully.",
                ResponseData = categories
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryListQuery>(q => q.SearchKeyword == null && q.IsActive == true && q.Limit == 20 && q.Offset == 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.All(result.ResponseData, c => Assert.True(c.IsActive));
            Assert.Equal("Categories retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategories_FilterByIsActiveFalse_ReturnsInactiveCategories()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, false, 20, 0);
            var categories = new List<CategoryDto>
            {
                new CategoryDto { CategoryId = _validCategoryId, Name = "Category 1", Slug = "category-1", Description = "Description 1", IsActive = false, CreatedAt = DateTime.UtcNow, UpdatedAt = null }
            };
            var expectedResponse = new BaseResponseDto<IEnumerable<CategoryDto>>
            {
                Status = 200,
                Message = "Categories retrieved successfully.",
                ResponseData = categories
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryListQuery>(q => q.SearchKeyword == null && q.IsActive == false && q.Limit == 20 && q.Offset == 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.All(result.ResponseData, c => Assert.False(c.IsActive));
            Assert.Equal("Categories retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategories_PaginationWithLimitAndOffset_ReturnsCorrectSubset()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 1, 1);
            var categories = new List<CategoryDto>
            {
                new CategoryDto { CategoryId = Guid.NewGuid(), Name = "Category 1", Slug = "category-1", Description = "Description 1", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
                new CategoryDto { CategoryId = _validCategoryId, Name = "Category 2", Slug = "category-2", Description = "Description 2", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            };
            var expectedResponse = new BaseResponseDto<IEnumerable<CategoryDto>>
            {
                Status = 200,
                Message = "Categories retrieved successfully.",
                ResponseData = categories.Skip(1).Take(1)
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryListQuery>(q => q.SearchKeyword == null && q.IsActive == null && q.Limit == 1 && q.Offset == 1), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Single(result.ResponseData);
            Assert.Equal("Category 2", result.ResponseData.First().Name);
            Assert.Equal("Categories retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategories_InvalidLimit_ReturnsBadRequest()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 0, 0);
            var expectedResponse = new BaseResponseDto<IEnumerable<CategoryDto>>
            {
                Status = 400,
                Message = "Limit must be greater than 0.",
                ResponseData = null
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryListQuery>(q => q.Limit == 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("Limit must be greater than 0.", result.Message);
        }

        [Fact]
        public async Task GetCategories_LargeNumberOfCategoriesWithPagination_ReturnsSuccess()
        {
            // Arrange
            var query = new GetCategoryListQuery(null, null, 20, 0);
            var categories = new List<CategoryDto>();
            for (int i = 0; i < 100; i++)
            {
                categories.Add(new CategoryDto
                {
                    CategoryId = Guid.NewGuid(),
                    Name = $"Category {i}",
                    Slug = $"category-{i}",
                    Description = $"Description {i}",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }
            var expectedResponse = new BaseResponseDto<IEnumerable<CategoryDto>>
            {
                Status = 200,
                Message = "Categories retrieved successfully.",
                ResponseData = categories.Take(20)
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryListQuery>(q => q.SearchKeyword == null && q.IsActive == null && q.Limit == 20 && q.Offset == 0), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategories(query);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Equal(20, result.ResponseData.Count());
            Assert.Equal("Categories retrieved successfully.", result.Message);
        }
    }
}