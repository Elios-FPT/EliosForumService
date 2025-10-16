using ForumService.Contract.Message;
using ForumService.Contract.Shared;
using ForumService.Web.Controllers.Category;
using MediatR;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using static ForumService.Contract.UseCases.Category.Request;
using static ForumService.Contract.UseCases.Category.Command;
using static ForumService.Contract.UseCases.Category.Request;

namespace ForumService.Tests.Controllers.CategoryController
{
    public class DeleteCategoryTests
    {
        private readonly Mock<ISender> _senderMock;
        private readonly Web.Controllers.Category.CategoryController _controller;
        private readonly Guid _validCategoryId = Guid.Parse("8cf071b9-ea2e-4a19-865e-28ec04a26ba7");
        private readonly Guid _nonExistentCategoryId = Guid.NewGuid();

        public DeleteCategoryTests()
        {
            _senderMock = new Mock<ISender>();
            _controller = new Web.Controllers.Category.CategoryController(_senderMock.Object);
        }

        [Fact]
        public async Task DeleteCategory_ValidId_ReturnsSuccess()
        {
            // Arrange
            var request = new DeleteCategoryRequest(_validCategoryId);
            var expectedResponse = new BaseResponseDto<bool>
            {
                Status = 200,
                Message = "Category deleted successfully.",
                ResponseData = true
            };
            _senderMock.Setup(s => s.Send(It.Is<DeleteCategoryCommand>(c => c.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Category deleted successfully.", result.Message);
        }

        [Fact]
        public async Task DeleteCategory_NonExistentId_ReturnsNotFound()
        {
            // Arrange
            var request = new DeleteCategoryRequest(_nonExistentCategoryId);
            var expectedResponse = new BaseResponseDto<bool>
            {
                Status = 404,
                Message = "Category not found.",
                ResponseData = false
            };
            _senderMock.Setup(s => s.Send(It.Is<DeleteCategoryCommand>(c => c.CategoryId == _nonExistentCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(404, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Category not found.", result.Message);
        }

        [Fact]
        public async Task DeleteCategory_EmptyId_ReturnsBadRequest()
        {
            // Arrange
            var request = new DeleteCategoryRequest(Guid.Empty);
            var expectedResponse = new BaseResponseDto<bool>
            {
                Status = 400,
                Message = "Invalid CategoryId.",
                ResponseData = false
            };
            _senderMock.Setup(s => s.Send(It.Is<DeleteCategoryCommand>(c => c.CategoryId == Guid.Empty), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Invalid CategoryId.", result.Message);
        }

        [Fact]
        public async Task DeleteCategory_ThrowsException_ReturnsInternalServerError()
        {
            // Arrange
            var request = new DeleteCategoryRequest(_validCategoryId);
            _senderMock.Setup(s => s.Send(It.IsAny<DeleteCategoryCommand>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Database error"));

            // Act
            var result = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.StartsWith("Failed to delete category: Database error", result.Message);
        }

        [Fact]
        public async Task DeleteCategory_NullResponseFromHandler_ReturnsInternalServerError()
        {
            // Arrange
            var request = new DeleteCategoryRequest(_validCategoryId);
            _senderMock.Setup(s => s.Send(It.Is<DeleteCategoryCommand>(c => c.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync((BaseResponseDto<bool>)null);

            // Act
            var result = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(500, result.Status);
            Assert.False(result.ResponseData);
            Assert.StartsWith("Failed to delete category:", result.Message);
        }

        [Fact]
        public async Task DeleteCategory_ActiveCategory_ReturnsSuccess()
        {
            // Arrange
            var request = new DeleteCategoryRequest(_validCategoryId);
            var expectedResponse = new BaseResponseDto<bool>
            {
                Status = 200,
                Message = "Category deleted successfully.",
                ResponseData = true
            };
            _senderMock.Setup(s => s.Send(It.Is<DeleteCategoryCommand>(c => c.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Category deleted successfully.", result.Message);
        }

        [Fact]
        public async Task DeleteCategory_InactiveCategory_ReturnsSuccess()
        {
            // Arrange
            var request = new DeleteCategoryRequest(_validCategoryId);
            var expectedResponse = new BaseResponseDto<bool>
            {
                Status = 200,
                Message = "Category deleted successfully.",
                ResponseData = true
            };
            _senderMock.Setup(s => s.Send(It.Is<DeleteCategoryCommand>(c => c.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(200, result.Status);
            Assert.True(result.ResponseData);
            Assert.Equal("Category deleted successfully.", result.Message);
        }

        [Fact]
        public async Task DeleteCategory_SameIdMultipleCalls_ReturnsConsistentResult()
        {
            // Arrange
            var request = new DeleteCategoryRequest(_validCategoryId);
            var expectedResponse = new BaseResponseDto<bool>
            {
                Status = 200,
                Message = "Category deleted successfully.",
                ResponseData = true
            };
            _senderMock.Setup(s => s.Send(It.Is<DeleteCategoryCommand>(c => c.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result1 = await _controller.DeleteCategory(request);
            var result2 = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result1);
            Assert.NotNull(result2);
            Assert.Equal(200, result1.Status);
            Assert.Equal(200, result2.Status);
            Assert.True(result1.ResponseData);
            Assert.True(result2.ResponseData);
            Assert.Equal("Category deleted successfully.", result1.Message);
            Assert.Equal("Category deleted successfully.", result2.Message);
        }

        [Fact]
        public async Task DeleteCategory_ValidIdWithRelatedData_ReturnsFailure()
        {
            // Arrange
            var request = new DeleteCategoryRequest(_validCategoryId);
            var expectedResponse = new BaseResponseDto<bool>
            {
                Status = 400,
                Message = "Cannot delete category because it has related data.",
                ResponseData = false
            };
            _senderMock.Setup(s => s.Send(It.Is<DeleteCategoryCommand>(c => c.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedResponse);

            // Act
            var result = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(400, result.Status);
            Assert.False(result.ResponseData);
            Assert.Equal("Cannot delete category because it has related data.", result.Message);
        }

        [Fact]
        public async Task DeleteCategory_ValidIdAfterPreviousDeletion_ReturnsNotFound()
        {
            // Arrange
            var request = new DeleteCategoryRequest(_validCategoryId);
            var successResponse = new BaseResponseDto<bool>
            {
                Status = 200,
                Message = "Category deleted successfully.",
                ResponseData = true
            };
            var notFoundResponse = new BaseResponseDto<bool>
            {
                Status = 404,
                Message = "Category not found.",
                ResponseData = false
            };
            _senderMock.SetupSequence(s => s.Send(It.Is<DeleteCategoryCommand>(c => c.CategoryId == _validCategoryId), It.IsAny<CancellationToken>()))
                .ReturnsAsync(successResponse)
                .ReturnsAsync(notFoundResponse);

            // Act
            var result1 = await _controller.DeleteCategory(request);
            var result2 = await _controller.DeleteCategory(request);

            // Assert
            Assert.NotNull(result1);
            Assert.Equal(200, result1.Status);
            Assert.True(result1.ResponseData);
            Assert.Equal("Category deleted successfully.", result1.Message);

            Assert.NotNull(result2);
            Assert.Equal(404, result2.Status);
            Assert.False(result2.ResponseData);
            Assert.Equal("Category not found.", result2.Message);
        }
    }
}