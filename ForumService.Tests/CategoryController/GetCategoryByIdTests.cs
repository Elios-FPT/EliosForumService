using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Category;
using ForumService.Web.Controllers.Category;
using MediatR;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Category.Query;
using static ForumService.Contract.UseCases.Category.Request;

namespace ForumService.Tests.Controllers.CategoryController
{
    public class GetCategoryByIdTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Category.CategoryController _controller;
        private readonly Guid _validCategoryId = Guid.Parse("8cf071b9-ea2e-4a19-865e-28ec04a26ba7");
        private readonly Guid _nonExistentCategoryId = Guid.NewGuid();

        public GetCategoryByIdTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Category.CategoryController(_senderMock.Object);
        }

        [Fact]
        public async Task GetCategoryById_ValidId_ReturnsSuccess()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(_validCategoryId);
            var category = new CategoryDto
            {
                CategoryId = _validCategoryId,
                Name = "Category 1",
                Slug = "category-1",
                Description = "Description 1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var expectedResponse = new BaseResponseDto<CategoryDto>
            {
                Status = 200,
                Message = "Category retrieved successfully.",
                ResponseData = category
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryByIdQuery>(q => q.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Equal(_validCategoryId, result.ResponseData.CategoryId);
            Assert.Equal("Category 1", result.ResponseData.Name);
            Assert.Equal("Category retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategoryById_NonExistentId_ReturnsNotFound()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(_nonExistentCategoryId);
            var expectedResponse = new BaseResponseDto<CategoryDto>
            {
                Status = 404,
                Message = "Category not found.",
                ResponseData = null
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryByIdQuery>(q => q.CategoryId == _nonExistentCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("Category not found.", result.Message);
        }

        [Fact]
        public async Task GetCategoryById_EmptyId_ReturnsBadRequest()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(Guid.Empty);
            var expectedResponse = new BaseResponseDto<CategoryDto>
            {
                Status = 400,
                Message = "Invalid CategoryId.",
                ResponseData = null
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryByIdQuery>(q => q.CategoryId == Guid.Empty), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.Null(result.ResponseData);
            Assert.Equal("Invalid CategoryId.", result.Message);
        }

        [Fact]
        public async Task GetCategoryById_ThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(_validCategoryId);
            _senderMock.Setup(s => s.Send(It.IsAny<GetCategoryByIdQuery>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.Status);
            Assert.Null(result.ResponseData);
            Assert.StartsWith("Failed to retrieve category: Database error", result.Message);
        }

        [Fact]
        public async Task GetCategoryById_NullResponseFromHandler_ReturnsInternalServerError()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(_validCategoryId);
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryByIdQuery>(q => q.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BaseResponseDto<CategoryDto>)null);

            // Act
            var result = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.Status);
            Assert.Null(result.ResponseData);
            Assert.StartsWith("Failed to retrieve category:", result.Message);
        }

        [Fact]
        public async Task GetCategoryById_ActiveCategory_ReturnsSuccess()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(_validCategoryId);
            var category = new CategoryDto
            {
                CategoryId = _validCategoryId,
                Name = "Category 1",
                Slug = "category-1",
                Description = "Description 1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var expectedResponse = new BaseResponseDto<CategoryDto>
            {
                Status = 200,
                Message = "Category retrieved successfully.",
                ResponseData = category
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryByIdQuery>(q => q.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.True(result.ResponseData.IsActive);
            Assert.Equal("Category retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategoryById_InactiveCategory_ReturnsSuccess()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(_validCategoryId);
            var category = new CategoryDto
            {
                CategoryId = _validCategoryId,
                Name = "Category 1",
                Slug = "category-1",
                Description = "Description 1",
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
            var expectedResponse = new BaseResponseDto<CategoryDto>
            {
                Status = 200,
                Message = "Category retrieved successfully.",
                ResponseData = category
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryByIdQuery>(q => q.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.False(result.ResponseData.IsActive);
            Assert.Null(result.ResponseData.UpdatedAt);
            Assert.Equal("Category retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategoryById_CategoryWithoutDescription_ReturnsSuccess()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(_validCategoryId);
            var category = new CategoryDto
            {
                CategoryId = _validCategoryId,
                Name = "Category 1",
                Slug = "category-1",
                Description = null,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var expectedResponse = new BaseResponseDto<CategoryDto>
            {
                Status = 200,
                Message = "Category retrieved successfully.",
                ResponseData = category
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryByIdQuery>(q => q.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Null(result.ResponseData.Description);
            Assert.Equal("Category retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategoryById_ValidIdWithNoUpdatedAt_ReturnsSuccess()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(_validCategoryId);
            var category = new CategoryDto
            {
                CategoryId = _validCategoryId,
                Name = "Category 1",
                Slug = "category-1",
                Description = "Description 1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };
            var expectedResponse = new BaseResponseDto<CategoryDto>
            {
                Status = 200,
                Message = "Category retrieved successfully.",
                ResponseData = category
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryByIdQuery>(q => q.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.NotNull(result.ResponseData);
            Assert.Null(result.ResponseData.UpdatedAt);
            Assert.Equal("Category retrieved successfully.", result.Message);
        }

        [Fact]
        public async Task GetCategoryById_SameIdMultipleCalls_ReturnsConsistentResult()
        {
            // Arrange
            var request = new GetCategoryByIdRequest(_validCategoryId);
            var category = new CategoryDto
            {
                CategoryId = _validCategoryId,
                Name = "Category 1",
                Slug = "category-1",
                Description = "Description 1",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var expectedResponse = new BaseResponseDto<CategoryDto>
            {
                Status = 200,
                Message = "Category retrieved successfully.",
                ResponseData = category
            };
            _senderMock.Setup(s => s.Send(It.Is<GetCategoryByIdQuery>(q => q.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result1 = await _controller.GetCategoryById(request);
            var result2 = await _controller.GetCategoryById(request);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(200, result1.Status);
            Assert.Equal(200, result2.Status);
            Assert.NotNull(result1.ResponseData);
            Assert.NotNull(result2.ResponseData);
            Assert.Equal(result1.ResponseData.CategoryId, result2.ResponseData.CategoryId);
            Assert.Equal("Category retrieved successfully.", result1.Message);
            Assert.Equal("Category retrieved successfully.", result2.Message);
        }
    }
}