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

namespace ForumService.Tests.Controllers.CategoryController
{
    public class UpdateCategoryTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Category.CategoryController _controller;
        private readonly Guid _validCategoryId = Guid.Parse("8cf071b9-ea2e-4a19-865e-28ec04a26ba7");

        public UpdateCategoryTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Category.CategoryController(_senderMock.Object);
        }

        [Fact]
        public async Task UpdateCategory_ValidRequest_ReturnsSuccess()
        {
            // Arrange
            var request = new UpdateCategoryRequest(_validCategoryId, "Updated Category", "Updated Description", false);
            var command = new UpdateCategoryCommand(_validCategoryId, request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Category updated successfully.", ResponseData = true };
            _senderMock.Setup(s => s.Send(It.Is<UpdateCategoryCommand>(c => c.CategoryId == _validCategoryId && c.Name == request.Name), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateCategory(_validCategoryId,request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Category updated successfully.", result.Message);
        }

        [Fact]
        public async Task UpdateCategory_CategoryNotFound_ReturnsNotFound()
        {
            // Arrange
            var request = new UpdateCategoryRequest(_validCategoryId, "Updated Category", "Updated Description", false);
            var command = new UpdateCategoryCommand(_validCategoryId, request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 404, Message = "Category not found.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<UpdateCategoryCommand>(c => c.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateCategory(_validCategoryId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category not found.", result.Message);
        }

        [Fact]
        public async Task UpdateCategory_EmptyName_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateCategoryRequest(_validCategoryId, "", "Updated Description", false);
            var command = new UpdateCategoryCommand(_validCategoryId, request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Category name is required.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<UpdateCategoryCommand>(c => c.Name == ""), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateCategory(_validCategoryId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category name is required.", result.Message);
        }

        [Fact]
        public async Task UpdateCategory_NullName_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateCategoryRequest(_validCategoryId, null, "Updated Description", false);
            var command = new UpdateCategoryCommand(_validCategoryId, request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Category name is required.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<UpdateCategoryCommand>(c => c.Name == null), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateCategory(_validCategoryId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category name is required.", result.Message);
        }

        [Fact]
        public async Task UpdateCategory_DuplicateName_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateCategoryRequest(_validCategoryId, "Existing Category", "Updated Description", false);
            var command = new UpdateCategoryCommand(_validCategoryId, request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Category name already exists.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<UpdateCategoryCommand>(c => c.Name == request.Name), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateCategory(_validCategoryId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category name already exists.", result.Message);
        }

        [Fact]
        public async Task UpdateCategory_NameTooLong_ReturnsBadRequest()
        {
            // Arrange
            var longName = new string('A', 101); // Vượt quá giới hạn 100 ký tự
            var request = new UpdateCategoryRequest(_validCategoryId, longName, "Updated Description", false);
            var command = new UpdateCategoryCommand(_validCategoryId, request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Category name is too long.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<UpdateCategoryCommand>(c => c.Name == longName), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateCategory(_validCategoryId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category name is too long.", result.Message);
        }

        [Fact]
        public async Task UpdateCategory_DescriptionTooLong_ReturnsBadRequest()
        {
            // Arrange
            var longDescription = new string('A', 1001); // Giả sử giới hạn là 1000 ký tự
            var request = new UpdateCategoryRequest(_validCategoryId, "Updated Category", longDescription, false);
            var command = new UpdateCategoryCommand(_validCategoryId, request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Description is too long.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<UpdateCategoryCommand>(c => c.Description == longDescription), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateCategory(_validCategoryId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Description is too long.", result.Message);
        }

        [Fact]
        public async Task UpdateCategory_NullDescription_ReturnsSuccess()
        {
            // Arrange
            var request = new UpdateCategoryRequest(_validCategoryId, "Updated Category", null, false);
            var command = new UpdateCategoryCommand(_validCategoryId, request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 200, Message = "Category updated successfully.", ResponseData = true };
            _senderMock.Setup(s => s.Send(It.Is<UpdateCategoryCommand>(c => c.Name == request.Name && c.Description == null), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateCategory(_validCategoryId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Category updated successfully.", result.Message);
        }

        [Fact]
        public async Task UpdateCategory_InvalidCategoryId_ReturnsBadRequest()
        {
            // Arrange
            var request = new UpdateCategoryRequest(_validCategoryId, "Updated Category", "Updated Description", false);
            var command = new UpdateCategoryCommand(Guid.Empty, request.Name, request.Description, request.IsActive);
            var expectedResponse = new BaseResponseDto<bool> { Status = 400, Message = "Invalid CategoryId.", ResponseData = false };
            _senderMock.Setup(s => s.Send(It.Is<UpdateCategoryCommand>(c => c.CategoryId == Guid.Empty), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.UpdateCategory(Guid.Empty, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Invalid CategoryId.", result.Message);
        }

        [Fact]
        public async Task UpdateCategory_ThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new UpdateCategoryRequest(_validCategoryId, "Updated Category", "Updated Description", false);
            var command = new UpdateCategoryCommand(_validCategoryId, request.Name, request.Description, request.IsActive);
            _senderMock.Setup(s => s.Send(It.IsAny<UpdateCategoryCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.UpdateCategory(_validCategoryId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.StartsWith("Failed to update category: Database error", result.Message);
        }
    }
}