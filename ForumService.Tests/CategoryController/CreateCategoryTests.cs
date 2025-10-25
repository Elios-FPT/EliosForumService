using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Contract.TransferObjects.Category;
using ForumService.Web.Controllers.Category;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Category.Command;
using static ForumService.Contract.UseCases.Category.Request;
using static ForumService.Contract.UseCases.Category.Query;

namespace ForumService.Tests.Controllers.CategoryController
{
    public class CreateCategoryTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Category.CategoryController _controller;

        public CreateCategoryTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Category.CategoryController(_senderMock.Object);
        }

        [Fact]
        public async Task CreateCategory_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new CreateCategoryRequest("Test Category", "Test Description", true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Category created successfully.", ResponseData = true };
            _senderMock.Setup(s => s.Send(It.Is<CreateCategoryCommand>(c => c.Name == request.Name), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Category created successfully.", result.Message);
        }

        [Fact]
        public async Task CreateCategory_EmptyName_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateCategoryRequest("", "Test Description", true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Category name is required.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<CreateCategoryCommand>(c => c.Name == ""), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category name is required.", result.Message);
        }

        [Fact]
        public async Task CreateCategory_NullName_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateCategoryRequest(null, "Test Description", true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Category name is required.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<CreateCategoryCommand>(c => c.Name == null), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category name is required.", result.Message);
        }

        [Fact]
        public async Task CreateCategory_DuplicateName_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateCategoryRequest("Existing Category", "Test Description", true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Category name already exists.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<CreateCategoryCommand>(c => c.Name == request.Name), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category name already exists.", result.Message);
        }

        [Fact]
        public async Task CreateCategory_NameTooLong_ReturnsBadRequest()
        {
            // Arrange
            var longName = new string('A', 101); // Giả sử giới hạn là 100 ký tự
            var request = new CreateCategoryRequest(longName, "Test Description", true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Category name is too long.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<CreateCategoryCommand>(c => c.Name == longName), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category name is too long.", result.Message);
        }

        [Fact]
        public async Task CreateCategory_DescriptionTooLong_ReturnsBadRequest()
        {
            // Arrange
            var longDescription = new string('A', 1001); // Giả sử giới hạn là 1000 ký tự
            var request = new CreateCategoryRequest("Test Category", longDescription, true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Description is too long.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<CreateCategoryCommand>(c => c.Description == longDescription), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Description is too long.", result.Message);
        }

        [Fact]
        public async Task CreateCategory_NullDescription_ReturnsSuccess()
        {
            // Arrange
            var request = new CreateCategoryRequest("Test Category", null, true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Category created successfully.", ResponseData = true };
            _senderMock.Setup(s => s.Send(It.Is<CreateCategoryCommand>(c => c.Name == request.Name && c.Description == null), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Category created successfully.", result.Message);
        }

        [Fact]
        public async Task CreateCategory_EmptyDescription_ReturnsSuccess()
        {
            // Arrange
            var request = new CreateCategoryRequest("Test Category", "", true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Category created successfully.", ResponseData = true };
            _senderMock.Setup(s => s.Send(It.Is<CreateCategoryCommand>(c => c.Name == request.Name && c.Description == ""), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Category created successfully.", result.Message);
        }

        [Fact]
        public async Task CreateCategory_SpecialCharactersInName_ReturnsBadRequest()
        {
            // Arrange
            var request = new CreateCategoryRequest("Test@#$Category", "Test Description", true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Category name contains invalid characters.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<CreateCategoryCommand>(c => c.Name == request.Name), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category name contains invalid characters.", result.Message);
        }

        [Fact]
        public async Task CreateCategory_ThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new CreateCategoryRequest("Test Category", "Test Description", true);
            var command = new CreateCategoryCommand(request.Name, request.Description, request.IsActive);
            _senderMock.Setup(s => s.Send(It.IsAny<CreateCategoryCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.CreateCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.StartsWith("Failed to create category: Database error", result.Message);
        }
    }
}
